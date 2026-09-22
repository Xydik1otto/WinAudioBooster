using System;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace WinAudioBooster.Audio
{
    public class AudioEngine : IDisposable
    {
        private WasapiLoopbackCapture? _capture;
        private WasapiOut? _output;
        private BufferedWaveProvider? _waveBuffer;
        private DspProcessor? _dspProcessor;
        private MediaFoundationResampler? _resampler;

        public DspProcessor? Dsp => _dspProcessor;
        public bool IsRunning { get; private set; }

        public event EventHandler<string>? ErrorOccurred;

        public void Start(MMDevice? captureDevice, MMDevice? outputDevice)
        {
            Stop();

            if (outputDevice == null) return;

            try
            {
                _capture = captureDevice != null 
                    ? new WasapiLoopbackCapture(captureDevice) 
                    : new WasapiLoopbackCapture();

                _waveBuffer = new BufferedWaveProvider(_capture.WaveFormat)
                {
                    DiscardOnBufferOverflow = true,
                    BufferDuration = TimeSpan.FromMilliseconds(500),
                    ReadFully = true
                };

                _capture.DataAvailable += (s, e) =>
                {
                    if (_waveBuffer != null && e.BytesRecorded > 0)
                    {
                        _waveBuffer.AddSamples(e.Buffer, 0, e.BytesRecorded);
                    }
                };

                ISampleProvider sampleProvider = _waveBuffer.ToSampleProvider();
                _dspProcessor = new DspProcessor(sampleProvider);

                var outputMixFormat = outputDevice.AudioClient.MixFormat;
                IWaveProvider finalWaveProvider = _dspProcessor.ToWaveProvider();

                if (_dspProcessor.WaveFormat.SampleRate != outputMixFormat.SampleRate ||
                    _dspProcessor.WaveFormat.Channels != outputMixFormat.Channels)
                {
                    var targetFormat = new WaveFormat(outputMixFormat.SampleRate, 16, outputMixFormat.Channels);
                    _resampler = new MediaFoundationResampler(finalWaveProvider, targetFormat)
                    {
                        ResamplerQuality = 30 // Баланс якості та швидкодії CPU
                    };
                    finalWaveProvider = _resampler;
                }

                // Низька затримка буфера 40 мс без тріску
                _output = new WasapiOut(outputDevice, AudioClientShareMode.Shared, false, 40);
                _output.Init(finalWaveProvider);

                _output.Play();
                _capture.StartRecording();

                IsRunning = true;
            }
            catch (Exception ex)
            {
                Stop();
                ErrorOccurred?.Invoke(this, $"Помилка аудіопотоку: {ex.Message}");
            }
        }

        public void Stop()
        {
            IsRunning = false;

            if (_capture != null)
            {
                try { _capture.StopRecording(); } catch { }
                _capture.Dispose();
                _capture = null;
            }

            if (_output != null)
            {
                try { _output.Stop(); } catch { }
                _output.Dispose();
                _output = null;
            }

            if (_resampler != null)
            {
                _resampler.Dispose();
                _resampler = null;
            }

            _waveBuffer = null;
            _dspProcessor = null;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}