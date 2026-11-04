namespace WinAudioBooster.Audio;

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

    public AudioEngine()
    {
        try { MediaFoundationApi.Startup(); } catch { }
    }

    public void Start(MMDevice captureCableDevice, MMDevice outputSpeakerDevice)
    {
        Stop();

        if (captureCableDevice == null || outputSpeakerDevice == null)
        {
            ErrorOccurred?.Invoke(this, "Не вибрано пристрій кабелю або фізичні динаміки.");
            return;
        }

        try
        {
            _capture = new WasapiLoopbackCapture(captureCableDevice);

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

            ISampleProvider sampleProvider = _waveBuffer.ToSampleProvider();
            _dspProcessor = new DspProcessor(sampleProvider);

            var outputMixFormat = outputSpeakerDevice.AudioClient.MixFormat;
            IWaveProvider finalWaveProvider;

            if (_dspProcessor.WaveFormat.SampleRate != outputMixFormat.SampleRate ||
                _dspProcessor.WaveFormat.Channels != outputMixFormat.Channels)
            {
                _resampler = new MediaFoundationResampler(_dspProcessor.ToWaveProvider(), outputMixFormat)
                {
                    ResamplerQuality = 30
                };
                finalWaveProvider = _resampler;
            }
            else if (outputMixFormat.BitsPerSample == 16)
            {
                finalWaveProvider = new SampleToWaveProvider16(_dspProcessor);
            }
            else
            {
                finalWaveProvider = _dspProcessor.ToWaveProvider();
            }

            _output = new WasapiOut(outputSpeakerDevice, AudioClientShareMode.Shared, false, 50);
            _output.PlaybackStopped += (s, e) =>
            {
                if (e.Exception != null)
                    ErrorOccurred?.Invoke(this, $"Помилка відтворення WASAPI: {e.Exception.Message}");
            };

            _output.Init(finalWaveProvider);

            _capture.StartRecording();
            _output.Play();

            IsRunning = true;
        }
        catch (Exception ex)
        {
            Stop();
            ErrorOccurred?.Invoke(this, $"Помилка ініціалізації WASAPI: {ex.Message}");
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

    public void Dispose() => Stop();
}