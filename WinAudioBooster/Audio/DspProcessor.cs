using NAudio.Dsp;

namespace WinAudioBooster.Audio;

/// <summary>
/// DSP-ланцюжок: посилення -> 10-смуговий еквалайзер -> тон-корекції ->
/// stereo widen -> crossfeed -> mono/balance -> компресор -> loudness normalization (AGC) -> лімітер.
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

    // Додаткові тон-корекції: суб-бас HPF, бас-буст (low shelf), treble-буст (high shelf).
    // Порядок у ланцюжку: гейн -> EQ -> [суб-бас HPF -> бас-буст -> treble-буст] -> компресор -> лімітер.
    public const float SubBassFilterFrequencyHz = 30f;
    public const float BassBoostFrequencyHz = 90f;
    public const float BassBoostGainDb = 8f;
    public const float TrebleBoostFrequencyHz = 9000f;
    public const float TrebleBoostGainDb = 6f;
    private const float ExtraFilterQualityFactor = 0.7f; // Q суб-бас HPF
    private const float ShelfSlope = 1f; // shelfSlope NAudio: 1 = максимально плавний shelf (аналог Q≈0.7)
    private const int ExtraFilterCount = 3; // HPF, бас-буст, treble-буст

    private bool _subBassFilterEnabled;
    private bool _bassBoostEnabled;
    private bool _trebleBoostEnabled;
    private bool _extraFiltersDirty = true;
    private BiQuadFilter?[] _extraFilters = Array.Empty<BiQuadFilter>(); // [filter * channels + channel]; null = фільтр вимкнено
    private int _extraSampleRate = -1;
    private int _extraChannels = -1;

    public LevelMeter InputMeter { get; } = new();
    public LevelMeter OutputMeter { get; } = new();

    public WaveFormat WaveFormat => _source.WaveFormat;
    public bool IsEnabled { get; set; } = true;

    public bool IsEqEnabled { get; set; } = true;

    // Гейн рухається до цілі з обмеженою швидкістю: 10 дБ за ~170 мс —
    // зміни слайдера відчуті миттєво, але без клацань на різких рухах.
    private const float GainSlewRateDbPerSecond = 60f;

    private float _gainDb = 0f;        // цільовий гейн (дБ); запис лише змінює ціль
    private float _appliedGainDb = 0f; // поточний застосований гейн (дБ)
    private float _appliedGainLinear = 1.0f;

    /// <summary>
    /// Цільовий гейн у дБ. Запис змінює лише ціль: фактичний гейн плавно
    /// рухається до неї зі швидкістю GainSlewRateDbPerSecond (slew limiting),
    /// тому різкі рухи слайдера не дають клацань. Швидкість підйому вибрана
    /// так, щоб типова зміна (<10 дБ) відчувалася миттєво (<200 мс).
    /// </summary>
    public float GainDb
    {
        get => _gainDb;
        set => _gainDb = value;
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

    #region Бас-буст, treble-буст і суб-бас фільтр

    /// <summary>Суб-бас HPF: зрізає сигнал нижче ~30 Гц (гул, рокот, інфразвук).</summary>
    public bool IsSubBassFilterEnabled
    {
        get => _subBassFilterEnabled;
        set => SetExtraToggle(ref _subBassFilterEnabled, value);
    }

    /// <summary>Бас-буст: low shelf на ~90 Гц, +8 дБ.</summary>
    public bool IsBassBoostEnabled
    {
        get => _bassBoostEnabled;
        set => SetExtraToggle(ref _bassBoostEnabled, value);
    }

    /// <summary>Treble-буст: high shelf на ~9 кГц, +6 дБ.</summary>
    public bool IsTrebleBoostEnabled
    {
        get => _trebleBoostEnabled;
        set => SetExtraToggle(ref _trebleBoostEnabled, value);
    }

    /// <summary>Спільний сетер прапорців: змінює значення та позначає фільтри як застарілі.</summary>
    private void SetExtraToggle(ref bool field, bool value)
    {
        if (field == value) return;

        lock (_eqLock)
        {
            if (field == value) return;
            field = value;
            _extraFiltersDirty = true;
        }
    }

    /// <summary>Будує BiQuad-фільтри додаткових тон-корекцій для кожного каналу.</summary>
    private void RebuildExtraFilters(int sampleRate, int channels)
    {
        if (sampleRate <= 0 || channels <= 0) return;

        var filters = new BiQuadFilter?[ExtraFilterCount * channels];
        for (int ch = 0; ch < channels; ch++)
        {
            if (_subBassFilterEnabled)
            {
                float freq = Math.Min(SubBassFilterFrequencyHz, sampleRate * 0.45f);
                filters[0 * channels + ch] = BiQuadFilter.HighPassFilter(sampleRate, freq, ExtraFilterQualityFactor);
            }

            if (_bassBoostEnabled)
            {
                float freq = Math.Min(BassBoostFrequencyHz, sampleRate * 0.45f);
                filters[1 * channels + ch] = BiQuadFilter.LowShelf(sampleRate, freq, ShelfSlope, BassBoostGainDb);
            }

            if (_trebleBoostEnabled)
            {
                float freq = Math.Min(TrebleBoostFrequencyHz, sampleRate * 0.45f);
                filters[2 * channels + ch] = BiQuadFilter.HighShelf(sampleRate, freq, ShelfSlope, TrebleBoostGainDb);
            }
        }

        _extraFilters = filters;
        _extraSampleRate = sampleRate;
        _extraChannels = channels;
        _extraFiltersDirty = false;
    }

    /// <summary>Гарантує, що додаткові фільтри відповідають прапорцям і формату (частота/канали).</summary>
    private void EnsureExtraFiltersCurrent(int sampleRate, int channels)
    {
        if (!_extraFiltersDirty && _extraSampleRate == sampleRate && _extraChannels == channels) return;
        RebuildExtraFilters(sampleRate, channels);
    }

    #endregion

    #region Loudness normalization (спрощений EBU R128, RMS-based AGC)

    // Однополюсний згладжувач RMS з постійною часу ~1 с — значно повільніше за
    // компресор, щоб нормалізація не пульсувала слідом за музикою.
    private const float AgcTimeConstantSeconds = 1.0f;
    private const float AgcTargetRms = 0.12f;          // цільовий RMS (лінійний), ~ -18 дБFS
    private const float AgcMaxBoostDb = 12f;           // межа підйому: +12 дБ
    private const float AgcMaxCutDb = 6f;              // межа притискання: -6 дБ
    private const float AgcSlewRateDbPerSecond = 5f;   // гейн рухається не швидше за 5 дБ/с
    private const float AgcMinRms = 1e-6f;             // підлога для логарифмування

    private bool _isLoudnessNormalizationEnabled;
    private float _agcRms;     // згладжений RMS (лінійний) сигналу на вході AGC
    private float _agcGainDb;  // поточний гейн AGC (дБ), з slew limiting

    /// <summary>
    /// Авто-гейн за цільовим RMS (спрощений EBU R128): повільно підтягує
    /// короткочасний рівень до ~0.12 (≈ -18 дБFS). Працює ПІСЛЯ всіх інших
    /// стадій (перед лімітером), додаючи власний множник поверх ручного GainDb.
    /// Корекція обмежена +12/-6 дБ і рухається плавно (≤5 дБ/с), тому не дає
    /// стрибків гучності. При вимкненні гейн плавно повертається до 1.
    /// </summary>
    public bool IsLoudnessNormalizationEnabled
    {
        get => _isLoudnessNormalizationEnabled;
        set
        {
            if (_isLoudnessNormalizationEnabled == value) return;
            _isLoudnessNormalizationEnabled = value;
            // Після увімкнення починаємо вимірювання рівня з нуля (застаріла
            // огинаюча дала б неправильну корекцію). Гейн НЕ скидаємо — він
            // плавно прийде до цілі зі slew limiting, без стрибка.
            _agcRms = 0f;
        }
    }

    /// <summary>Цільовий RMS-рівень у дБ (спрощено замість повноцінного LUFS).</summary>
    public float LoudnessTargetDb { get; set; } = (float)(20 * Math.Log10(AgcTargetRms));

    /// <summary>
    /// Рухає гейн AGC до цілі з обмеженням швидкості (5 дБ/с) і повертає
    /// лінійний множник. targetDb &lt; 0 означає відсутність цілі (AGC вимкнено) —
    /// тоді гейн плавно повертається до 0 дБ (єдениці).
    /// </summary>
    private float AdvanceAgcSlew(float? targetDb, float stepDb)
    {
        float target = targetDb ?? 0f;
        float delta = target - _agcGainDb;
        if (Math.Abs(delta) <= stepDb)
        {
            _agcGainDb = target;
        }
        else
        {
            _agcGainDb += Math.Sign(delta) * stepDb;
        }

        return (float)Math.Pow(10, _agcGainDb / 20.0);
    }

    #endregion

    #region Crossfeed (для навушників)

    // Класичний crossfeed: у кожен канал підмішується копія протилежного каналу,
    // затримана на ~0.3 мс, пропущена через LPF ~700 Гц і змішана на ~25%.
    public const float CrossfeedDelaySeconds = 0.0003f;
    public const float CrossfeedFrequencyHz = 700f;
    public const float CrossfeedMix = 0.25f;
    private const float CrossfeedLowPassQ = 0.7f;

    private bool _isCrossfeedEnabled;
    private BiQuadFilter[] _crossfeedFilters = Array.Empty<BiQuadFilter>(); // [канал 0/1] — LPF ~700 Гц
    private float[] _crossfeedDelayL = Array.Empty<float>(); // кільцевий буфер затримки лівого каналу
    private float[] _crossfeedDelayR = Array.Empty<float>(); // кільцевий буфер затримки правого каналу
    private int _crossfeedDelayPos;
    private int _crossfeedSampleRate = -1;
    private bool _crossfeedDirty = true;

    /// <summary>
    /// Crossfeed для навушників: протилежний канал (затримка ~0.3 мс + LPF ~700 Гц)
    /// підмішується на ~30%. Для моно — no-op.
    /// </summary>
    public bool IsCrossfeedEnabled
    {
        get => _isCrossfeedEnabled;
        set
        {
            if (_isCrossfeedEnabled == value) return;
            _isCrossfeedEnabled = value;
            _crossfeedDirty = true; // перестворюємо фільтри та чистимо лінії затримки
        }
    }

    private void RebuildCrossfeed(int sampleRate)
    {
        if (sampleRate <= 0) return;

        int delaySamples = Math.Max(1, (int)MathF.Round(sampleRate * CrossfeedDelaySeconds));
        _crossfeedDelayL = new float[delaySamples];
        _crossfeedDelayR = new float[delaySamples];
        _crossfeedDelayPos = 0;

        float cutoff = Math.Min(CrossfeedFrequencyHz, sampleRate * 0.45f);
        _crossfeedFilters = new[]
        {
            BiQuadFilter.LowPassFilter(sampleRate, cutoff, CrossfeedLowPassQ),
            BiQuadFilter.LowPassFilter(sampleRate, cutoff, CrossfeedLowPassQ),
        };
        _crossfeedSampleRate = sampleRate;
        _crossfeedDirty = false;
    }

    /// <summary>Гарантує, що crossfeed-фільтри та затримки відповідають частоті дискретизації.</summary>
    private void EnsureCrossfeedCurrent(int sampleRate)
    {
        if (!_crossfeedDirty && _crossfeedSampleRate == sampleRate) return;
        RebuildCrossfeed(sampleRate);
    }

    /// <summary>
    /// Обробляє один фрейм crossfeed-ом (канали 0/1). Лінії затримки містять
    /// вхідні (до crossfeed) семпли каналу — чиста feed-forward структура, без накопичення.
    /// </summary>
    private void ApplyCrossfeed(float[] buffer, int frameStart)
    {
        float leftIn = buffer[frameStart];
        float rightIn = buffer[frameStart + 1];

        // Найстаріші семпли протилежних каналів -> LPF ~700 Гц
        float delayedR = _crossfeedDelayR[_crossfeedDelayPos];
        float delayedL = _crossfeedDelayL[_crossfeedDelayPos];
        float feedL = _crossfeedFilters[0].Transform(delayedR);
        float feedR = _crossfeedFilters[1].Transform(delayedL);

        buffer[frameStart] = leftIn + CrossfeedMix * feedL;
        buffer[frameStart + 1] = rightIn + CrossfeedMix * feedR;

        // У затримку пишемо вхідні семпли (feed-forward), а не виходи
        _crossfeedDelayL[_crossfeedDelayPos] = leftIn;
        _crossfeedDelayR[_crossfeedDelayPos] = rightIn;
        _crossfeedDelayPos++;
        if (_crossfeedDelayPos >= _crossfeedDelayL.Length) _crossfeedDelayPos = 0;
    }

    #endregion

    #region Stereo width, mono, balance

    /// <summary>
    /// Stereo widening (M/S): side-сигнал підсилюється у StereoWidenFactor разу —
    /// стереобаза стає ширшою без зміни mid-контенту. Для моно — no-op.
    /// </summary>
    public bool IsStereoWidenEnabled { get; set; }

    /// <summary>Множник side-сигналу при увімкненому stereo widening.</summary>
    public const float StereoWidenFactor = 1.5f;

    /// <summary>Stereo width (0..2, 1 = норма): M/S-обробка стерео-фрейму.</summary>
    public float StereoWidth { get; set; } = 1.0f;

    /// <summary>Моно-згортка: обидва канали фрейму стають (L+R)/2.</summary>
    public bool IsMonoEnabled { get; set; }

    /// <summary>
    /// Баланс (-1..+1): при Balance &gt; 0 правий канал гучністю 1, лівий —
    /// (1 - Balance); при Balance &lt; 0 навпаки. Повзунок балансу L/R на UI.
    /// </summary>
    public float Balance { get; set; }

    private static void ApplyStereoWidth(float[] buffer, int frameStart, float width)
    {
        float left = buffer[frameStart];
        float right = buffer[frameStart + 1];
        float mid = (left + right) / 2f;
        float side = (left - right) / 2f * width;
        buffer[frameStart] = mid + side;
        buffer[frameStart + 1] = mid - side;
    }

    private static void ApplyMono(float[] buffer, int frameStart, int channels)
    {
        float sum = 0f;
        for (int ch = 0; ch < channels; ch++) sum += buffer[frameStart + ch];
        float mono = sum / channels;
        for (int ch = 0; ch < channels; ch++) buffer[frameStart + ch] = mono;
    }

    /// <summary>
    /// Баланс: Balance &gt; 0 — правий канал без змін, лівий множиться на
    /// (1 - Balance); Balance &lt; 0 — навпаки ((1 + Balance) на правому).
    /// </summary>
    private static void ApplyBalance(float[] buffer, int frameStart, float balance)
    {
        if (balance > 0f)
        {
            buffer[frameStart] *= 1f - balance; // праворуч: приглушуємо лівий
        }
        else if (balance < 0f)
        {
            buffer[frameStart + 1] *= 1f + balance; // ліворуч: приглушуємо правий
        }
    }

    #endregion

    #region Спектральний тап візуалізатора (після лімітера, 4096 семплів)

    /// <summary>Розмір кільцевого буфера спектрального тапа візуалізатора (моно-семпли).</summary>
    public const int SpectrumBufferSize = 4096;

    // Буфер перевиділяється лише при зміні формату (під lock); у Read() жодних алокацій.
    private float[] _spectrumBuffer = new float[SpectrumBufferSize];
    private int _spectrumWritePos;  // індекс наступного запису в кільцевий буфер
    private int _spectrumFilled;    // скільки валідних семплів накопичено (<= SpectrumBufferSize)
    private int _spectrumSampleRate = -1;
    private int _spectrumChannels = -1;
    private readonly object _spectrumBufferLock = new();

    /// <summary>
    /// Увімкнення спектрального тапа. Коли вимкнено, Read() не підтримує
    /// буфер (заощаджує CPU), а CopySpectrumSamples повертає 0.
    /// </summary>
    public bool IsSpectrumTapEnabled { get; set; } = true;

    /// <summary>Перевиділяє буфер тапа при зміні формату (частота/канали) — старі семпли скидаються.</summary>
    private void EnsureSpectrumBufferCurrent(int sampleRate, int channels)
    {
        if (_spectrumSampleRate == sampleRate && _spectrumChannels == channels) return;

        _spectrumBuffer = new float[SpectrumBufferSize];
        _spectrumWritePos = 0;
        _spectrumFilled = 0;
        _spectrumSampleRate = sampleRate;
        _spectrumChannels = channels;
    }

    /// <summary>
    /// Копіює останні семпли моно-міксу фінального (після лімітера) звуку у
    /// destination у хронологічному порядку (найстаріші першими) — не більше
    /// destination.Length. Повертає кількість скопійованих семплів (0, коли
    /// тап вимкнено або ще нічого не накопичено). Потокобезпечно: короткий
    /// lock навколо O(N)-копіювання, без алокацій — безпечно кликати з
    /// UI-таймера (~33 мс).
    /// </summary>
    public int CopySpectrumSamples(float[] destination)
    {
        if (destination == null) throw new ArgumentNullException(nameof(destination));

        lock (_spectrumBufferLock)
        {
            if (!IsSpectrumTapEnabled) return 0;

            int count = Math.Min(destination.Length, _spectrumFilled);
            var src = _spectrumBuffer;
            // Найстаріший валідний семпл лежить за (writePos - filled) модульно
            for (int i = 0; i < count; i++)
            {
                destination[i] = src[(_spectrumWritePos - _spectrumFilled + i + SpectrumBufferSize) % SpectrumBufferSize];
            }

            return count;
        }
    }

    /// <summary>Дописує моно-мікс фінальних (оброблених) фреймів у кільцевий буфер тапа.</summary>
    private void FillSpectrumBuffer(float[] buffer, int offset, int sampleCount)
    {
        if (!IsSpectrumTapEnabled) return;

        int channels = Math.Max(1, WaveFormat.Channels);
        int frames = sampleCount / channels;
        if (frames <= 0) return;

        lock (_spectrumBufferLock)
        {
            EnsureSpectrumBufferCurrent(WaveFormat.SampleRate, channels);

            var ring = _spectrumBuffer;
            int pos = _spectrumWritePos;
            for (int frame = 0; frame < frames; frame++)
            {
                int frameStart = offset + frame * channels;
                float mono = buffer[frameStart];
                for (int ch = 1; ch < channels; ch++)
                {
                    mono += buffer[frameStart + ch];
                }

                ring[pos] = mono / channels;
                pos++;
                if (pos >= SpectrumBufferSize) pos = 0;
            }

            _spectrumWritePos = pos;
            _spectrumFilled = Math.Min(SpectrumBufferSize, _spectrumFilled + frames);
        }
    }

    #endregion

    public int Read(float[] buffer, int offset, int count)
    {
        int samplesRead = _source.Read(buffer, offset, count);
        if (samplesRead == 0) return 0;

        InputMeter.ProcessSamples(buffer, offset, samplesRead);

        if (!IsEnabled)
        {
            // Коли DSP вимкнено, гейн все одно "доганяє" ціль, щоб після
            // повторного увімкнення не було стрибка гучності.
            AdvanceGainSlew(samplesRead);
            FillSpectrumBuffer(buffer, offset, samplesRead); // пас-тру: семпли все одно йдуть у тап
            OutputMeter.ProcessSamples(buffer, offset, samplesRead);
            return samplesRead;
        }

        int sampleRate = WaveFormat.SampleRate;
        int channels = Math.Max(1, WaveFormat.Channels);

        // Локальні копії параметрів + фільтрів, щоб UI-потік не міг підмінити масив посеред обробки
        BiQuadFilter[] eqFilters;
        BiQuadFilter?[] extraFilters;
        lock (_eqLock)
        {
            EnsureEqFiltersCurrent(sampleRate, channels);
            EnsureExtraFiltersCurrent(sampleRate, channels);
            eqFilters = _eqFilters;
            extraFilters = _extraFilters;
        }

        bool useEq = IsEqEnabled && eqFilters.Length == BandCount * channels;
        bool useExtra = extraFilters.Length == ExtraFilterCount * channels;
        bool useCompressor = IsCompressorEnabled;
        bool useLimiter = IsLimiterEnabled;

        // Нові етапи ланцюжка: widen -> crossfeed -> mono/balance (перед компресором),
        // потім AGC (loudness normalization) після компресора, перед лімітером.
        bool useLoudness = IsLoudnessNormalizationEnabled;
        bool useCrossfeed = IsCrossfeedEnabled && channels >= 2;
        // Widen: M/S-множник = StereoWidth * (widen ? 1.5 : 1); 1 = блок пропускається.
        float widenFactor = Math.Clamp(StereoWidth, 0f, 2f) * (IsStereoWidenEnabled ? StereoWidenFactor : 1f);
        bool useStereoWiden = channels >= 2 && Math.Abs(widenFactor - 1f) > 1e-4f;
        bool useMono = IsMonoEnabled && channels >= 2;
        float balance = Math.Clamp(Balance, -1f, 1f);
        bool useBalance = channels >= 2 && Math.Abs(balance) > 1e-4f;
        // Швидкий шлях: без widen/crossfeed/mono/balance блок не виконується і нічого не копіюється.
        bool useSpatial = useCrossfeed || useStereoWiden || useMono || useBalance;

        // Коефіцієнт однополюсного згладжувача RMS для AGC (постійна часу ~1 с)
        float agcCoef = (float)Math.Exp(-1.0 / (sampleRate * AgcTimeConstantSeconds));
        // Крок зміни гейну AGC за один фрейм (дБ): 5 дБ/с * тривалість фрейму
        float agcStepDb = AgcSlewRateDbPerSecond * channels / sampleRate;
        float loudnessTargetDb = LoudnessTargetDb;

        if (useCrossfeed) EnsureCrossfeedCurrent(sampleRate);

        float gain = _appliedGainLinear;
        float targetGainDb = _gainDb;
        float appliedGainDb = _appliedGainDb;
        // Крок зміни гейну за один фрейм (дБ): 60 дБ/с * тривалість фрейму.
        // Рахуємо з кількості фреймів, без таймерів.
        float gainStepDb = GainSlewRateDbPerSecond * channels / sampleRate;
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
            // 0. Плавний гейн: рухаємося до цілі з обмеженою швидкістю.
            // Рух у дБ-домені (лінійний по дБ) дає геометричну огибающую
            // амплітуди — плавно на слух на будь-якій глибині зміни.
            if (appliedGainDb != targetGainDb)
            {
                float delta = targetGainDb - appliedGainDb;
                if (Math.Abs(delta) <= gainStepDb)
                {
                    appliedGainDb = targetGainDb;
                }
                else
                {
                    appliedGainDb += Math.Sign(delta) * gainStepDb;
                }
                gain = (float)Math.Pow(10, appliedGainDb / 20.0);
            }

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

                // Додаткові тон-корекції: суб-бас HPF -> бас-буст -> treble-буст (null = вимкнено)
                if (useExtra)
                {
                    for (int f = 0; f < ExtraFilterCount; f++)
                    {
                        var extraFilter = extraFilters[f * channels + ch];
                        if (extraFilter != null) sample = extraFilter.Transform(sample);
                    }
                }

                buffer[frameStart + ch] = sample;

                float abs = Math.Abs(sample);
                if (abs > framePeak) framePeak = abs;
            }

            // 1.5 Просторова обробка: stereo widen -> crossfeed -> mono -> balance
            if (useSpatial)
            {
                if (useStereoWiden) ApplyStereoWidth(buffer, frameStart, widenFactor);
                if (useCrossfeed) ApplyCrossfeed(buffer, frameStart);
                if (useMono) ApplyMono(buffer, frameStart, channels);
                if (useBalance) ApplyBalance(buffer, frameStart, balance);
            }

            // Пік перераховуємо після просторової обробки — компресор бачить фактичний рівень
            if (useSpatial)
            {
                framePeak = 0f;
                for (int ch = 0; ch < channels; ch++)
                {
                    float abs = Math.Abs(buffer[frameStart + ch]);
                    if (abs > framePeak) framePeak = abs;
                }
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

            // 3. Loudness normalization (AGC): після всіх інших стадій, перед лімітером.
            // Однополюсний згладжувач RMS (~1 с) тягне рівень до цілі; корекція
            // обмежена +12/-6 дБ, гейн рухається не швидше за 5 дБ/с.
            // Коли AGC вимкнено, але гейн ще не повернувся до 1 — продовжуємо
            // плавний рух до 0 дБ (без стрибка).
            if (useLoudness || _agcGainDb != 0f)
            {
                float? agcTargetDb = null;
                if (useLoudness)
                {
                    // RMS поточного фрейму після компресора (те, що бачить AGC)
                    float sumSquares = 0f;
                    for (int ch = 0; ch < channels; ch++)
                    {
                        float s = buffer[frameStart + ch];
                        sumSquares += s * s;
                    }

                    float frameRms = (float)Math.Sqrt(sumSquares / channels);
                    _agcRms = agcCoef * _agcRms + (1f - agcCoef) * frameRms;

                    float envelopeDb = 20f * (float)Math.Log10(Math.Max(_agcRms, AgcMinRms));
                    agcTargetDb = Math.Clamp(loudnessTargetDb - envelopeDb, -AgcMaxCutDb, AgcMaxBoostDb);
                }

                float agcGain = AdvanceAgcSlew(agcTargetDb, agcStepDb);
                for (int ch = 0; ch < channels; ch++)
                {
                    buffer[frameStart + ch] *= agcGain;
                }
            }

            // 4. М'який tanh-лімітер: пік ніколи не перевищує стелю
            if (useLimiter)
            {
                for (int ch = 0; ch < channels; ch++)
                {
                    buffer[frameStart + ch] = ApplySoftLimiter(buffer[frameStart + ch], ceilingLinear, knee);
                }
            }
        }

        // Зберігаємо стан гейну після обробки блоку
        _appliedGainDb = appliedGainDb;
        _appliedGainLinear = gain;

        // Спектральний тап заповнюємо найостаннішим — після всієї обробки (включно з лімітером)
        FillSpectrumBuffer(buffer, offset, samplesRead);

        OutputMeter.ProcessSamples(buffer, offset, samplesRead);
        return samplesRead;
    }

    /// <summary>
    /// Рухає застосований гейн до цілі на задану кількість семплів
    /// (використовується, коли DSP вимкнено і сигнал не обробляється).
    /// Заодно плавно повертає гейн AGC до 0 дБ, щоб після повторного
    /// увімкнення не було стрибка гучності.
    /// </summary>
    private void AdvanceGainSlew(int sampleCount)
    {
        int sampleRate = WaveFormat.SampleRate;
        float stepDb = GainSlewRateDbPerSecond * sampleCount / Math.Max(1, sampleRate);
        float delta = _gainDb - _appliedGainDb;

        if (Math.Abs(delta) <= stepDb)
        {
            _appliedGainDb = _gainDb;
        }
        else
        {
            _appliedGainDb += Math.Sign(delta) * stepDb;
        }

        _appliedGainLinear = (float)Math.Pow(10, _appliedGainDb / 20.0);

        // AGC вимкнено разом із DSP — гейн плавно повертається до єдиниці
        if (_agcGainDb != 0f)
        {
            float agcStepDb = AgcSlewRateDbPerSecond * sampleCount / Math.Max(1, sampleRate);
            AdvanceAgcSlew(null, agcStepDb);
        }
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
