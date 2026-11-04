using NAudio.Dsp;

namespace WinAudioBooster.Audio;

/// <summary>
/// DSP-ланцюжок: посилення -> 10-смуговий еквалайзер -> компресор -> м'який tanh-лімітер.
/// Обробка виконується по фреймах (з урахуванням кількості каналів WaveFormat.Channels),
/// щоб стерео/мультиканальний сигнал оброблявся коректно.
/// </summary>
public class DspProcessor : ISampleProvider
{
    // 10-смуговий еквалайзер (ISO 1/3-октавні центри)
    public static readonly float[] BandFrequencies = { 31f, 62f, 125f, 250f, 500f, 1000f, 2000f, 4000f, 8000f, 16000f };
    public const float MinBandGainDb = -12f;
    public const float MaxBandGainDb = 12f;
    public const int BandCount = 10;

    private const float EqQualityFactor = 1.2f;
    private const float LimiterKneeFactor = 0.9f; // коліно лімітера: 0.9 * ceiling

    private readonly ISampleProvider _source;

    // Гейни смуг EQ + фільтри BiQuad (окремий фільтр на кожну смугу та канал)
    private readonly float[] _eqBandGainsDb = new float[BandCount];
    private BiQuadFilter[] _eqFilters = Array.Empty<BiQuadFilter>(); // [band * channels + channel]
    private int _eqSampleRate = -1;
    private int _eqChannels = -1;
    private readonly object _eqLock = new();

    public LevelMeter InputMeter { get; } = new();
    public LevelMeter OutputMeter { get; } = new();

    public WaveFormat WaveFormat => _source.WaveFormat;
    public bool IsEnabled { get; set; } = true;

    public bool IsEqEnabled { get; set; } = true;

    private float _gainDb = 0f;
    private float _linearGain = 1.0f;
    public float GainDb
    {
        get => _gainDb;
        set
        {
            _gainDb = value;
            _linearGain = (float)Math.Pow(10, _gainDb / 20.0);
        }
    }

    public bool IsCompressorEnabled { get; set; } = true;
    public float CompressorThresholdDb { get; set; } = -20f;
    public float CompressorRatio { get; set; } = 3f;
    public float CompressorAttackMs { get; set; } = 10f;
    public float CompressorReleaseMs { get; set; } = 100f;

    private float _envelope = 0f;

    public bool IsLimiterEnabled { get; set; } = true;

    private const float DefaultLimiterCeilingDb = -0.3f;
    private float _limiterCeilingDb = DefaultLimiterCeilingDb;
    private float _limiterCeilingLinear = (float)Math.Pow(10, DefaultLimiterCeilingDb / 20.0);

    public float LimiterCeilingDb
    {
        get => _limiterCeilingDb;
        set
        {
            _limiterCeilingDb = value;
            // Кешуємо лінійне значення стелі, щоб не рахувати степінь на кожному семплі
            _limiterCeilingLinear = (float)Math.Pow(10, value / 20.0);
        }
    }

    public DspProcessor(ISampleProvider source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        RebuildEqFilters(WaveFormat.SampleRate, WaveFormat.Channels);
    }

    #region Еквалайзер

    public static bool IsValidBandIndex(int band) => band >= 0 && band < BandCount;

    /// <summary>Встановлює гейн смуги (дБ) та перераховує відповідні фільтри.</summary>
    public void SetEqBandGain(int band, float gainDb)
    {
        if (!IsValidBandIndex(band)) throw new ArgumentOutOfRangeException(nameof(band));

        gainDb = Math.Clamp(gainDb, MinBandGainDb, MaxBandGainDb);

        lock (_eqLock)
        {
            // Перерахунок лише коли гейн реально змінився
            if (Math.Abs(_eqBandGainsDb[band] - gainDb) < 1e-4f) return;

            _eqBandGainsDb[band] = gainDb;
            RebuildEqFilters(WaveFormat.SampleRate, WaveFormat.Channels);
        }
    }

    public float GetEqBandGain(int band)
    {
        if (!IsValidBandIndex(band)) throw new ArgumentOutOfRangeException(nameof(band));

        lock (_eqLock)
        {
            return _eqBandGainsDb[band];
        }
    }

    /// <summary>Скидає всі смуги EQ у 0 дБ (плоска АЧХ).</summary>
    public void ResetEq()
    {
        lock (_eqLock)
        {
            bool changed = false;
            for (int b = 0; b < BandCount; b++)
            {
                if (Math.Abs(_eqBandGainsDb[b]) > 1e-4f) changed = true;
                _eqBandGainsDb[b] = 0f;
            }

            if (changed)
            {
                RebuildEqFilters(WaveFormat.SampleRate, WaveFormat.Channels);
            }
        }
    }

    /// <summary>Будує BiQuad-фільтри PeakingEQ для кожної смуги та кожного каналу.</summary>
    private void RebuildEqFilters(int sampleRate, int channels)
    {
        if (sampleRate <= 0 || channels <= 0) return;

        var filters = new BiQuadFilter[BandCount * channels];
        for (int b = 0; b < BandCount; b++)
        {
            float gainDb = _eqBandGainsDb[b];
            // Високі частоти не мусять перевищувати частоту Найквіста
            float freq = Math.Min(BandFrequencies[b], sampleRate * 0.45f);
            for (int ch = 0; ch < channels; ch++)
            {
                filters[b * channels + ch] = BiQuadFilter.PeakingEQ(sampleRate, freq, EqQualityFactor, gainDb);
            }
        }

        _eqFilters = filters;
        _eqSampleRate = sampleRate;
        _eqChannels = channels;
    }

    /// <summary>Гарантує, що фільтри відповідають поточному формату (частота/канали).</summary>
    private void EnsureEqFiltersCurrent(int sampleRate, int channels)
    {
        if (_eqSampleRate == sampleRate && _eqChannels == channels) return;
        RebuildEqFilters(sampleRate, channels);
    }

    #endregion

    public int Read(float[] buffer, int offset, int count)
    {
        int samplesRead = _source.Read(buffer, offset, count);
        if (samplesRead == 0) return 0;

        InputMeter.ProcessSamples(buffer, offset, samplesRead);

        if (!IsEnabled)
        {
            OutputMeter.ProcessSamples(buffer, offset, samplesRead);
            return samplesRead;
        }

        int sampleRate = WaveFormat.SampleRate;
        int channels = Math.Max(1, WaveFormat.Channels);

        // Локальні копії параметрів + фільтрів, щоб UI-потік не міг підмінити масив посеред обробки
        BiQuadFilter[] eqFilters;
        lock (_eqLock)
        {
            EnsureEqFiltersCurrent(sampleRate, channels);
            eqFilters = _eqFilters;
        }

        bool useEq = IsEqEnabled && eqFilters.Length == BandCount * channels;
        bool useCompressor = IsCompressorEnabled;
        bool useLimiter = IsLimiterEnabled;

        float gain = _linearGain;
        float attackCoef = (float)Math.Exp(-1.0 / (sampleRate * (CompressorAttackMs / 1000.0)));
        float releaseCoef = (float)Math.Exp(-1.0 / (sampleRate * (CompressorReleaseMs / 1000.0)));
        float compressorThresholdDb = CompressorThresholdDb;
        float compressorRatio = CompressorRatio;

        float ceilingLinear = Math.Max(_limiterCeilingLinear, 1e-4f);
        float knee = LimiterKneeFactor * ceilingLinear;

        // Обробка по фреймах: один фрейм = channels семплів (по одному на канал)
        int frameCount = samplesRead / channels;

        for (int frame = 0; frame < frameCount; frame++)
        {
            int frameStart = offset + frame * channels;

            // 1. Boost + EQ (по каналах)
            float framePeak = 0f;
            for (int ch = 0; ch < channels; ch++)
            {
                float sample = buffer[frameStart + ch] * gain;

                if (useEq)
                {
                    for (int b = 0; b < BandCount; b++)
                    {
                        sample = eqFilters[b * channels + ch].Transform(sample);
                    }
                }

                buffer[frameStart + ch] = sample;

                float abs = Math.Abs(sample);
                if (abs > framePeak) framePeak = abs;
            }

            // 2. Компресор (огинаюча рахується по піку фрейму, діє на всі канали однаково)
            if (useCompressor)
            {
                _envelope = framePeak > _envelope
                    ? attackCoef * _envelope + (1 - attackCoef) * framePeak
                    : releaseCoef * _envelope + (1 - releaseCoef) * framePeak;

                float envDb = 20f * (float)Math.Log10(Math.Max(_envelope, 1e-6f));

                if (envDb > compressorThresholdDb)
                {
                    float overDb = envDb - compressorThresholdDb;
                    float compressedDb = compressorThresholdDb + (overDb / compressorRatio);
                    float gainReductionLinear = (float)Math.Pow(10, (compressedDb - envDb) / 20.0);

                    for (int ch = 0; ch < channels; ch++)
                    {
                        buffer[frameStart + ch] *= gainReductionLinear;
                    }
                }
            }

            // 3. М'який tanh-лімітер: пік ніколи не перевищує стелю
            if (useLimiter)
            {
                for (int ch = 0; ch < channels; ch++)
                {
                    buffer[frameStart + ch] = ApplySoftLimiter(buffer[frameStart + ch], ceilingLinear, knee);
                }
            }
        }

        OutputMeter.ProcessSamples(buffer, offset, samplesRead);
        return samplesRead;
    }

    /// <summary>
    /// М'який лімітер: нижче коліна сигнал проходить без змін,
    /// вище — плавно стискається через tanh так, що асимптота дорівнює стелі.
    /// </summary>
    private static float ApplySoftLimiter(float sample, float ceilingLinear, float knee)
    {
        float abs = Math.Abs(sample);
        if (abs <= knee) return sample;

        float over = abs - knee;
        float compressed = knee + (ceilingLinear - knee) * (float)Math.Tanh(over / (ceilingLinear - knee));
        return Math.Sign(sample) * compressed;
    }
}
