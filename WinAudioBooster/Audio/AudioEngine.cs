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
                // 1. Ініціалізація захоплення
                _capture = captureDevice != null 
                    ? new WasapiLoopbackCapture(captureDevice) 
                    : new WasapiLoopbackCapture();

                // 2. Створення стабільного буфера з ReadFully = true (Усуває тріск)
                _waveBuffer = new BufferedWaveProvider(_capture.WaveFormat)
                {
                    DiscardOnBufferOverflow = true,
                    BufferDuration = TimeSpan.FromSeconds(2),
                    ReadFully = true
                };

                _capture.DataAvailable += (s, e) =>
                {
                    if (_waveBuffer != null && e.BytesRecorded > 0)
                    {
                        _waveBuffer.AddSamples(e.Buffer, 0, e.BytesRecorded);
                    }
                };

                // 3. Конвертація у SampleProvider
                ISampleProvider sampleProvider = _waveBuffer.ToSampleProvider();

                // 4. Ініціалізація DSP
                _dspProcessor = new DspProcessor(sampleProvider);

                // 5. Отримання нативної частоти вихідного пристрою та ресемплінг за потреби
                var outputMixFormat = outputDevice.AudioClient.MixFormat;
                IWaveProvider finalWaveProvider = _dspProcessor.ToWaveProvider();

                if (_dspProcessor.WaveFormat.SampleRate != outputMixFormat.SampleRate ||
                    _dspProcessor.WaveFormat.Channels != outputMixFormat.Channels)
                {
                    var targetFormat = new WaveFormat(outputMixFormat.SampleRate, 16, outputMixFormat.Channels);
                    _resampler = new MediaFoundationResampler(finalWaveProvider, targetFormat)
                    {
                        ResamplerQuality = 60 // Виправлена назва властивості у NAudio
                    };
                    finalWaveProvider = _resampler;
                }

                // 6. Ініціалізація виводу з затримкою 80 мс для стабільності
                _output = new WasapiOut(outputDevice, AudioClientShareMode.Shared, false, 80);
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