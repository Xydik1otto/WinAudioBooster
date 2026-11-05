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

            // Буфер лише згладжує тремтіння таймерів: DataAvailable надходить
            // кожні ~10 мс, тому 250 мс вміщує запас на ~25 пропущених
            // колбеків. ReadFully=true дає тишу, якщо буфер усе ж спорожнів
            // (безпечний захист від underrun'ів замість 2 с накопиченої затримки).
            // NAudio 2.4+: BufferDuration лише для читання — розмір задається
            // конструктором (optional TimeSpan bufferDuration).
            _waveBuffer = new BufferedWaveProvider(_capture.WaveFormat, TimeSpan.FromMilliseconds(250))
            {
                DiscardOnBufferOverflow = true,
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

            // 30 мс замість 50: помітно нижча латентність і все ще безпечний
            // запас для shared-режиму. Низький пріоритет потоку відтворення
            // змінити не можна: WasapiOut створює власний потік усередині,
            // публічного доступу до нього NAudio не надає.
            _output = new WasapiOut(outputSpeakerDevice, AudioClientShareMode.Shared, false, 30);
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