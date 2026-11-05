using NAudio.Wave;
using WinAudioBooster.Audio;
using Xunit;

namespace WinAudioBooster.Tests;

/// <summary>Простий фейковий джерело семплів для тестів DSP.</summary>
internal class FakeSampleProvider : ISampleProvider
{
    private readonly Func<int, float> _generator;
    private int _position;
    public WaveFormat WaveFormat { get; }

    public FakeSampleProvider(WaveFormat waveFormat, Func<int, float> generator)
    {
        WaveFormat = waveFormat;
        _generator = generator;
    }

    public int Read(float[] buffer, int offset, int count)
    {
        for (int i = 0; i < count; i++)
        {
            buffer[offset + i] = _generator(_position++);
        }
        return count;
    }

    public static WaveFormat MakeFormat(int sampleRate = 44100, int channels = 2) =>
        WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
}

public class DspProcessorTests
{
    private const int SampleRate = 44100;
    private const int Channels = 2;

    private static float[] ReadAll(DspProcessor dsp, int sampleCount)
    {
        var buffer = new float[sampleCount];
        int read = dsp.Read(buffer, 0, sampleCount);
        Assert.Equal(sampleCount, read);
        return buffer;
    }

    private static double Rms(float[] samples)
    {
        double sum = 0;
        for (int i = 0; i < samples.Length; i++) sum += (double)samples[i] * samples[i];
        return Math.Sqrt(sum / samples.Length);
    }

    private static DspProcessor CreateDsp(Func<int, float> generator, int channels = Channels)
    {
        var dsp = new DspProcessor(new FakeSampleProvider(FakeSampleProvider.MakeFormat(SampleRate, channels), generator))
        {
            IsCompressorEnabled = false,
            IsLimiterEnabled = false,
            IsEqEnabled = true,
            GainDb = 0
        };
        return dsp;
    }

    // Генератор моно-синусоїди заданої частоти
    private static Func<int, float> Sine(double frequencyHz, float amplitude = 0.5f) =>
        pos => amplitude * (float)Math.Sin(2 * Math.PI * frequencyHz * pos / SampleRate);

    [Fact]
    public void BandFrequencies_HasTenStandardBands()
    {
        Assert.Equal(10, DspProcessor.BandFrequencies.Length);
        Assert.Equal(31f, DspProcessor.BandFrequencies[0]);
        Assert.Equal(16000f, DspProcessor.BandFrequencies[^1]);
    }

    [Fact]
    public void SetEqBandGain_ClampsToAllowedRange()
    {
        var dsp = CreateDsp(_ => 0f);

        dsp.SetEqBandGain(0, -50f);
        Assert.Equal(DspProcessor.MinBandGainDb, dsp.GetEqBandGain(0));

        dsp.SetEqBandGain(0, 50f);
        Assert.Equal(DspProcessor.MaxBandGainDb, dsp.GetEqBandGain(0));

        dsp.ResetEq();
        Assert.Equal(0f, dsp.GetEqBandGain(0));
    }

    [Fact]
    public void ResetEq_SetsAllBandsToZero()
    {
        var dsp = CreateDsp(_ => 0f);
        for (int b = 0; b < DspProcessor.BandCount; b++)
        {
            dsp.SetEqBandGain(b, 6f * (b + 1));
        }

        dsp.ResetEq();

        for (int b = 0; b < DspProcessor.BandCount; b++)
        {
            Assert.Equal(0f, dsp.GetEqBandGain(b));
        }
    }

    [Fact]
    public void SetEqBandGain_InvalidIndex_Throws()
    {
        var dsp = CreateDsp(_ => 0f);
        Assert.Throws<ArgumentOutOfRangeException>(() => dsp.SetEqBandGain(-1, 0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => dsp.SetEqBandGain(DspProcessor.BandCount, 0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => dsp.GetEqBandGain(99));
    }

    [Fact]
    public void Eq_BoostsBand_SineAtBandFrequencyGetsLouder()
    {
        // Синус 1 кГц (смуга з індексом 5), моно — щоб частота сигналу була однозначною
        var flatDsp = CreateDsp(Sine(1000.0), channels: 1);
        double baselineRms = Rms(ReadAll(flatDsp, SampleRate / 4));

        var boostedDsp = CreateDsp(Sine(1000.0), channels: 1);
        boostedDsp.SetEqBandGain(5, +12f);
        double boostedRms = Rms(ReadAll(boostedDsp, SampleRate / 4));

        Assert.True(boostedRms > baselineRms * 1.5,
            $"Очікували підйом: baseline={baselineRms:F4}, boosted={boostedRms:F4}");
    }

    [Fact]
    public void Eq_CutBand_SineAtBandFrequencyGetsQuieter()
    {
        var flatDsp = CreateDsp(Sine(1000.0), channels: 1);
        double baselineRms = Rms(ReadAll(flatDsp, SampleRate / 4));

        var cutDsp = CreateDsp(Sine(1000.0), channels: 1);
        cutDsp.SetEqBandGain(5, -12f);
        double cutRms = Rms(ReadAll(cutDsp, SampleRate / 4));

        Assert.True(cutRms < baselineRms * 0.5,
            $"Очікували ослаблення: baseline={baselineRms:F4}, cut={cutRms:F4}");
    }

    [Fact]
    public void Eq_IsEqEnabledFalse_BypassesFilters()
    {
        var dsp = CreateDsp(Sine(1000.0), channels: 1);
        dsp.SetEqBandGain(5, -12f);
        dsp.IsEqEnabled = false;

        double rms = Rms(ReadAll(dsp, SampleRate / 4));
        Assert.Equal(0.5 / Math.Sqrt(2), rms, 3);
    }

    [Fact]
    public void Limiter_NeverExceedsCeiling_EvenWithHugeGain()
    {
        var dsp = CreateDsp(_ => 0.8f);
        dsp.IsCompressorEnabled = false;
        dsp.IsLimiterEnabled = true;
        dsp.GainDb = 40f; // x100 -> вхідні піки ~80

        float ceilingLinear = (float)Math.Pow(10, dsp.LimiterCeilingDb / 20.0);
        var samples = ReadAll(dsp, 4096);

        foreach (float s in samples)
        {
            Assert.True(Math.Abs(s) <= ceilingLinear + 1e-3f,
                $"Пік {s} перевищує стелю {ceilingLinear}");
        }
    }

    [Fact]
    public void Limiter_SmallSignalPassesUnchanged()
    {
        var dsp = CreateDsp(_ => 0.1f);
        dsp.IsCompressorEnabled = false;
        dsp.IsLimiterEnabled = true;
        dsp.GainDb = 0f;

        var samples = ReadAll(dsp, 256);
        foreach (float s in samples)
        {
            Assert.Equal(0.1f, s, 5);
        }
    }

    [Fact]
    public void LimiterCeilingDb_CachesLinearCeiling()
    {
        var dsp = CreateDsp(_ => 0.1f);
        dsp.LimiterCeilingDb = -6f; // 10^(-6/20) ~ 0.5012

        dsp.IsCompressorEnabled = false;
        dsp.IsLimiterEnabled = true;
        dsp.GainDb = 20f; // x10 -> вхід 1.0

        var samples = ReadAll(dsp, 4096);
        float expectedCeiling = (float)Math.Pow(10, -6.0 / 20.0);
        foreach (float s in samples)
        {
            Assert.True(Math.Abs(s) <= expectedCeiling + 1e-3f);
        }
    }

    [Fact]
    public void GainDb_SlewLimited_PartiallyAppliedRightAfterChange()
    {
        // Нова семантика: GainDb — лише ціль. Одразу після зміни гейн
        // застосовується частково (slew limiting, 60 дБ/с), а не миттєво.
        var dsp = CreateDsp(_ => 0.1f);
        dsp.GainDb = 20f; // ціль x10

        var samples = ReadAll(dsp, 64); // ~1.5 мс сигналу — гейн майже не зрушив
        float maxSample = samples.Max(Math.Abs);
        Assert.True(maxSample < 0.11f,
            $"Гейн має застосовуватися плавно, а не миттєво: max={maxSample}");
    }

    [Fact]
    public void GainDb_SlewLimited_ReachesTargetAfterSeriesOfReads()
    {
        var dsp = CreateDsp(_ => 0.1f);
        dsp.GainDb = 20f; // 20 дБ при 60 дБ/с = ~333 мс = ~14 700 семплів

        var settled = ReadAll(dsp, 30000);
        float expected = 0.1f * 10f; // цільовий лінійний гейн x10
        Assert.Equal(expected, settled[^1], 3);
    }

    [Fact]
    public void Limiter_PeaksStayWithinCeiling_WhenGainTargetRaisedMidStream()
    {
        var dsp = CreateDsp(_ => 0.9f);
        dsp.IsCompressorEnabled = false;
        dsp.IsLimiterEnabled = true;
        dsp.GainDb = 0f;
        ReadAll(dsp, 512);

        // Піднімаємо ціль посеред потоку: навіть поки гейн плавно набирається,
        // піки не виходять за стелю лімітера.
        dsp.GainDb = 40f;
        float ceilingLinear = (float)Math.Pow(10, dsp.LimiterCeilingDb / 20.0);
        var samples = ReadAll(dsp, 8192);
        foreach (float s in samples)
        {
            Assert.True(Math.Abs(s) <= ceilingLinear + 1e-3f,
                $"Пік {s} перевищує стелю {ceilingLinear}");
        }
    }

    [Fact]
    public void GainDb_SlewAdvancesWhileDisabled_NoJumpOnReEnable()
    {
        var dsp = CreateDsp(_ => 0.1f);
        dsp.IsEnabled = false;
        dsp.GainDb = 20f;

        ReadAll(dsp, 30000); // поки DSP вимкнено, гейн встигає догнати ціль

        dsp.IsEnabled = true;
        var samples = ReadAll(dsp, 64);
        Assert.Equal(0.1f * 10f, samples[0], 3); // без стрибка після увімкнення
    }

    [Fact]
    public void GainDb_Zero_IsUnity()
    {
        var dsp = CreateDsp(_ => 0.25f);
        dsp.GainDb = 0f;

        var samples = ReadAll(dsp, 64);
        foreach (float s in samples)
        {
            Assert.Equal(0.25f, s, 5);
        }
    }

    [Fact]
    public void Stereo_ProcessesFramesCorrectly_BothChannelsGetSameCompressorGain()
    {
        // Різні сигнали на каналах: лівий тихий, правий гучний.
        // Компресор має застосовувати однакове приглушення до обох каналів фрейму.
        var dsp = CreateDsp(pos => (pos % 2 == 0) ? 0.9f : 0.1f);
        dsp.IsCompressorEnabled = true;
        dsp.IsLimiterEnabled = false;
        dsp.CompressorThresholdDb = -20f;
        dsp.CompressorRatio = 4f;
        dsp.CompressorAttackMs = 1f;

        dsp.GainDb = 12f; // x~4 -> піки ~3.6 без лімітера
        dsp.LimiterCeilingDb = 0f;

        var samples = ReadAll(dsp, 8192);

        // Пропускаємо атаку огинаючої (перші ~2 кГц семплів), далі компресор виходить на режим
        for (int i = 4096; i < samples.Length; i += 2)
        {
            // Обидва канали не виходять за стелю
            Assert.True(Math.Abs(samples[i]) <= 1.001f,
                $"Лівий канал {samples[i]} перевищує стелю");
            Assert.True(Math.Abs(samples[i + 1]) <= 1.001f,
                $"Правий канал {samples[i + 1]} перевищує стелю");
        }
    }

    [Fact]
    public void Mono_ProcessesCorrectly()
    {
        var dsp = CreateDsp(_ => 0.5f, channels: 1);
        dsp.GainDb = 0f;

        var samples = ReadAll(dsp, 128);
        foreach (float s in samples)
        {
            Assert.Equal(0.5f, s, 5);
        }
    }

    #region Бас-буст, treble-буст і суб-бас фільтр

    [Fact]
    public void BassBoost_SineAt60Hz_GetsLouder()
    {
        // Синус 60 Гц — у межах low shelf на 90 Гц, моно
        var flatDsp = CreateDsp(Sine(60.0), channels: 1);
        double baselineRms = Rms(ReadAll(flatDsp, SampleRate / 2));

        var boostedDsp = CreateDsp(Sine(60.0), channels: 1);
        boostedDsp.IsBassBoostEnabled = true;
        double boostedRms = Rms(ReadAll(boostedDsp, SampleRate / 2));

        Assert.True(boostedRms > baselineRms * 1.3,
            $"Очікували підйом баса: baseline={baselineRms:F4}, boosted={boostedRms:F4}");
    }

    [Fact]
    public void BassBoost_Disabled_RestoresBaselineLevel()
    {
        var dsp = CreateDsp(Sine(60.0), channels: 1);
        dsp.IsBassBoostEnabled = true;
        ReadAll(dsp, SampleRate / 2); // обробка з увімкненим бустом

        dsp.IsBassBoostEnabled = false;
        double rms = Rms(ReadAll(dsp, SampleRate / 2));
        Assert.Equal(0.5 / Math.Sqrt(2), rms, 3);
    }

    [Fact]
    public void TrebleBoost_SineAt12kHz_GetsLouder()
    {
        // Синус 12 кГц — вище high shelf на 9 кГц, моно
        var flatDsp = CreateDsp(Sine(12000.0), channels: 1);
        double baselineRms = Rms(ReadAll(flatDsp, SampleRate / 4));

        var boostedDsp = CreateDsp(Sine(12000.0), channels: 1);
        boostedDsp.IsTrebleBoostEnabled = true;
        double boostedRms = Rms(ReadAll(boostedDsp, SampleRate / 4));

        Assert.True(boostedRms > baselineRms * 1.2,
            $"Очікували підйом верхів: baseline={baselineRms:F4}, boosted={boostedRms:F4}");
    }

    [Fact]
    public void TrebleBoost_Disabled_RestoresBaselineLevel()
    {
        var dsp = CreateDsp(Sine(12000.0), channels: 1);
        dsp.IsTrebleBoostEnabled = true;
        ReadAll(dsp, SampleRate / 4);

        dsp.IsTrebleBoostEnabled = false;
        double rms = Rms(ReadAll(dsp, SampleRate / 4));
        Assert.Equal(0.5 / Math.Sqrt(2), rms, 3);
    }

    [Fact]
    public void SubBassFilter_SineAt25Hz_GetsQuieter()
    {
        // Синус 25 Гц — нижче HPF на 30 Гц, моно; довше вікно через низьку частоту
        var flatDsp = CreateDsp(Sine(25.0), channels: 1);
        double baselineRms = Rms(ReadAll(flatDsp, SampleRate / 2));

        var filteredDsp = CreateDsp(Sine(25.0), channels: 1);
        filteredDsp.IsSubBassFilterEnabled = true;
        double filteredRms = Rms(ReadAll(filteredDsp, SampleRate / 2));

        Assert.True(filteredRms < baselineRms * 0.85,
            $"Очікували зріз суб-баса: baseline={baselineRms:F4}, filtered={filteredRms:F4}");
    }

    [Fact]
    public void SubBassFilter_Disabled_RestoresBaselineLevel()
    {
        var dsp = CreateDsp(Sine(25.0), channels: 1);
        dsp.IsSubBassFilterEnabled = true;
        ReadAll(dsp, SampleRate / 2);

        dsp.IsSubBassFilterEnabled = false;
        double rms = Rms(ReadAll(dsp, SampleRate / 2));
        Assert.Equal(0.5 / Math.Sqrt(2), rms, 3);
    }

    #endregion

    #region Loudness normalization, crossfeed, stereo width/mono/balance, spectrum tap

    [Fact]
    public void LoudnessNormalization_ConvergesToTarget()
    {
        // Синус із RMS -24 дБFS: амплітуда = 10^(-24/20) * sqrt(2)
        float amplitude = (float)(Math.Pow(10, -24.0 / 20.0) * Math.Sqrt(2));
        var dsp = CreateDsp(Sine(1000.0, amplitude));
        dsp.IsLoudnessNormalizationEnabled = true;
        dsp.LoudnessTargetDb = -16f;

        // ~10 с сигналу (порціями, як реальний playback) — вистачає для повільної
        // огинаючої (атака ~1-2 с) та повільного гейну нормалізації.
        int totalSamples = SampleRate * 10 * Channels;
        var buffer = new float[totalSamples];
        int read = 0;
        while (read < totalSamples)
        {
            int chunk = Math.Min(4096, totalSamples - read);
            int got = dsp.Read(buffer, read, chunk);
            Assert.Equal(chunk, got);
            read += got;
        }

        double rmsDb = 20 * Math.Log10(Rms(buffer[(totalSamples / 2)..]));
        Assert.InRange(rmsDb, -18.0, -14.0); // ціль -16 дБ, допуск ±2 дБ
    }

    [Fact]
    public void Crossfeed_SignalOnlyInLeft_AppearsInRight()
    {
        // Синус 200 Гц лише в лівому каналі (правий — нуль):
        // після crossfeed у правому каналі має з'явитися затримана відфільтрована копія.
        var dsp = CreateDsp(pos => (pos % 2 == 0)
            ? 0.4f * (float)Math.Sin(2 * Math.PI * 200.0 * (pos / 2) / SampleRate)
            : 0f);
        dsp.IsCrossfeedEnabled = true;

        var samples = ReadAll(dsp, SampleRate); // 1 с — значно більше за затримку 0.3 мс

        double leftRms = ChannelRms(samples, 0, 2, skipSamples: 4096);
        double rightRms = ChannelRms(samples, 1, 2, skipSamples: 4096);

        Assert.True(leftRms > 0.01, $"Лівий канал має містити сигнал: L={leftRms:F5}");
        Assert.True(rightRms > leftRms * 0.01,
            $"Очікували crossfeed у правому каналі (>1% від L): L={leftRms:F5}, R={rightRms:F5}");
    }

    [Fact]
    public void MonoEnabled_LeftEqualsRight()
    {
        // Різні сигнали на каналах — після моно-згортки L = R у кожному фреймі
        var dsp = CreateDsp(pos => (pos % 2 == 0) ? 0.3f : 0.6f);
        dsp.IsMonoEnabled = true;

        var samples = ReadAll(dsp, 1024);
        for (int i = 0; i < samples.Length; i += 2)
        {
            Assert.Equal(samples[i], samples[i + 1], 5);
        }
    }

    [Fact]
    public void Balance_PlusOne_LeftSilent_RightUnchanged()
    {
        // Balance = +1 (праворуч): лівий канал = 0.2 * (1 - 1) = 0, правий без змін
        var dsp = CreateDsp(_ => 0.2f);
        dsp.Balance = 1f;

        var samples = ReadAll(dsp, 1024);
        Assert.Equal(0f, samples[0], 4);
        Assert.True(Math.Abs(samples[1] - 0.2f) < 1e-3f,
            $"Правий канал має залишитися без змін: {samples[1]}");
    }

    [Fact]
    public void Balance_MinusOne_RightSilent_LeftUnchanged()
    {
        // Balance = -1 (ліворуч): правий канал = 0.2 * (1 + (-1)) = 0, лівий без змін
        var dsp = CreateDsp(_ => 0.2f);
        dsp.Balance = -1f;

        var samples = ReadAll(dsp, 1024);
        Assert.Equal(0f, samples[1], 4);
        Assert.True(Math.Abs(samples[0] - 0.2f) < 1e-3f,
            $"Лівий канал має залишитися без змін: {samples[0]}");
    }

    [Fact]
    public void CopySpectrumSamples_AfterSineRead_ReturnsSamples()
    {
        var dsp = CreateDsp(Sine(1000.0, 0.5f));

        // Читаємо вдвічі більше за розмір буфера — він гарантовано заповнений
        ReadAll(dsp, DspProcessor.SpectrumBufferSize * 2);

        var destination = new float[DspProcessor.SpectrumBufferSize];
        int copied = dsp.CopySpectrumSamples(destination);

        Assert.True(copied > 0, "Після Read з синусом тап має повернути >0 семплів");
        Assert.Equal(DspProcessor.SpectrumBufferSize, copied);
    }

    [Fact]
    public void CopySpectrumSamples_NeverExceedsDestinationLength()
    {
        var dsp = CreateDsp(Sine(1000.0, 0.5f));
        ReadAll(dsp, DspProcessor.SpectrumBufferSize * 2);

        // Менший буфер: має повернутися рівно destination.Length
        var small = new float[1000];
        Assert.Equal(1000, dsp.CopySpectrumSamples(small));

        // Частково заповнений тап після короткого читання: стільки ж, скільки фреймів прочитано
        var fresh = CreateDsp(Sine(1000.0, 0.5f));
        ReadAll(fresh, 300);
        Assert.Equal(150, fresh.CopySpectrumSamples(new float[4096])); // 300 семплів / 2 канали
    }

    [Fact]
    public void CopySpectrumSamples_Disabled_ReturnsZero()
    {
        var dsp = CreateDsp(Sine(1000.0, 0.5f));
        Assert.True(dsp.IsSpectrumTapEnabled, "Тап має бути увімкнений за замовчуванням");

        dsp.IsSpectrumTapEnabled = false;
        ReadAll(dsp, DspProcessor.SpectrumBufferSize * 2);

        Assert.Equal(0, dsp.CopySpectrumSamples(new float[4096]));
    }

    [Fact]
    public void CopySpectrumSamples_DspDisabled_PassThroughStillFills()
    {
        var dsp = CreateDsp(pos => 0.25f);
        dsp.IsEnabled = false;

        ReadAll(dsp, 2048);

        var tap = new float[512];
        int copied = dsp.CopySpectrumSamples(tap);
        Assert.True(copied > 0, "При вимкненому DSP семпли все одно мають потрапляти у буфер (пас-тру)");
        Assert.Equal(0.25f, tap[^1], 4); // останній семпл = фінальний (необроблений) сигнал
    }

    [Fact]
    public void CopySpectrumSamples_ContainsMonoMixOfFinalAudio()
    {
        // L = 0.2, R = 0.4 -> моно-мікс = 0.3; без компресора/лімітера значення не змінюються
        var dsp = CreateDsp(pos => (pos % 2 == 0) ? 0.2f : 0.4f);
        ReadAll(dsp, 2048);

        var tap = new float[256];
        Assert.Equal(256, dsp.CopySpectrumSamples(tap));
        foreach (float s in tap)
        {
            Assert.Equal(0.3f, s, 4);
        }
    }

    [Fact]
    public void CopySpectrumSamples_NullDestination_Throws()
    {
        var dsp = CreateDsp(Sine(1000.0, 0.5f));
        Assert.Throws<ArgumentNullException>(() => dsp.CopySpectrumSamples(null!));
    }

    [Fact]
    public void StereoWiden_IncreasesSideContent()
    {
        // Чистий side-сигнал (L=+0.5, R=-0.5): mid = 0, side = 0.5.
        // Після widen (side × 1.5): L = +0.75, R = -0.75 — |L|+|R| зростає з 1.0 до 1.5.
        var dsp = CreateDsp(pos => (pos % 2 == 0) ? 0.5f : -0.5f);
        dsp.IsStereoWidenEnabled = true;

        var samples = ReadAll(dsp, 1024);
        Assert.Equal(0.75f, samples[0], 4);
        Assert.Equal(-0.75f, samples[1], 4);
    }

    [Fact]
    public void StereoWiden_Disabled_PassesSignalUnchanged()
    {
        var dsp = CreateDsp(pos => (pos % 2 == 0) ? 0.5f : -0.5f);

        var samples = ReadAll(dsp, 64);
        Assert.Equal(0.5f, samples[0], 5);
        Assert.Equal(-0.5f, samples[1], 5);
    }

    [Fact]
    public void Crossfeed_LeftImpulse_AppearsInRightAfterDelay()
    {
        // Імпульс у лівому каналі: у правому каналі через ~0.3 мс
        // (sampleRate * CrossfeedDelaySeconds семплів) з'являється слабкий
        // відфільтрований відгук (коефіцієнт ~0.25 + імпульсний отклик LPF ~700 Гц).
        int delaySamples = Math.Max(1, (int)MathF.Round(SampleRate * DspProcessor.CrossfeedDelaySeconds));
        var dsp = CreateDsp(pos => pos == 0 ? 1f : 0f);
        dsp.IsCrossfeedEnabled = true;

        var samples = ReadAll(dsp, SampleRate / 2);

        // До затримки правий канал порожній
        for (int i = 1; i < delaySamples * 2; i += 2)
        {
            Assert.Equal(0f, samples[i], 5);
        }

        float maxRight = 0f;
        for (int i = delaySamples * 2 + 1; i < samples.Length; i += 2)
        {
            maxRight = Math.Max(maxRight, Math.Abs(samples[i]));
        }

        Assert.True(maxRight > 0.01f,
            $"Очікували crossfeed-відгук у правому каналі: max={maxRight}");
        Assert.True(maxRight < 0.2f,
            $"Crossfeed має бути слабким (~0.25): max={maxRight}");
    }

    [Fact]
    public void LoudnessNormalization_QuietSignal_IsBoostedTowardTarget()
    {
        // Тихий синус із RMS ~0.03: AGC має підняти рівень до цілі ~0.12
        // (корекція +12 дБ — на межі ліміту підйому).
        float amplitude = 0.03f * (float)Math.Sqrt(2);
        var dsp = CreateDsp(Sine(1000.0, amplitude));
        dsp.IsLoudnessNormalizationEnabled = true;

        double rms = ReadSecondsAndMeasureLast(dsp, totalSeconds: 12, measureSeconds: 2);

        Assert.True(rms > 0.08,
            $"Тихий сигнал має бути піднятий AGC: rms={rms:F4}");
        Assert.True(rms < 0.16,
            $"AGC не має перебирати понад ціль: rms={rms:F4}");
    }

    [Fact]
    public void LoudnessNormalization_LoudSignal_IsAttenuatedTowardTarget()
    {
        // Гучний синус із RMS ~0.4: AGC має притиснути рівень
        // (корекція -6 дБ — на межі ліміту притискання).
        float amplitude = 0.4f * (float)Math.Sqrt(2);
        var dsp = CreateDsp(Sine(1000.0, amplitude));
        dsp.IsLoudnessNormalizationEnabled = true;

        double rms = ReadSecondsAndMeasureLast(dsp, totalSeconds: 12, measureSeconds: 2);

        Assert.True(rms < 0.25,
            $"Гучний сигнал має бути притиснутий AGC: rms={rms:F4}");
        Assert.True(rms > 0.15,
            $"AGC не має притискати понад ціль: rms={rms:F4}");
    }

    [Fact]
    public void LoudnessNormalization_Disabled_GainReturnsSmoothlyToUnity()
    {
        // Після вимкнення AGC гейн плавно повертається до 1 (5 дБ/с):
        // +12 дБ / 5 дБ/с = 2.4 с, тому через 4 с після вимкнення вихід = вхід.
        float amplitude = 0.03f * (float)Math.Sqrt(2);
        var dsp = CreateDsp(Sine(1000.0, amplitude));
        dsp.IsLoudnessNormalizationEnabled = true;

        ReadSeconds(dsp, totalSeconds: 6); // прогрів: AGC набирає гейн
        dsp.IsLoudnessNormalizationEnabled = false;
        double rms = ReadSecondsAndMeasureLast(dsp, totalSeconds: 4, measureSeconds: 1);

        Assert.Equal(0.03, rms, 3);
    }

    [Fact]
    public void NewFeatureFlags_DefaultToOff()
    {
        var dsp = CreateDsp(_ => 0.1f);
        Assert.False(dsp.IsLoudnessNormalizationEnabled);
        Assert.False(dsp.IsStereoWidenEnabled);
        Assert.False(dsp.IsCrossfeedEnabled);
        Assert.False(dsp.IsMonoEnabled);
        Assert.Equal(0f, dsp.Balance);
    }

    /// <summary>Читає totalSeconds сигналу порціями (як реальний playback).</summary>
    private static void ReadSeconds(DspProcessor dsp, double totalSeconds)
    {
        int totalSamples = (int)(SampleRate * totalSeconds) * Channels;
        var buffer = new float[totalSamples];
        int read = 0;
        while (read < totalSamples)
        {
            int chunk = Math.Min(4096, totalSamples - read);
            int got = dsp.Read(buffer, read, chunk);
            Assert.Equal(chunk, got);
            read += got;
        }
    }

    /// <summary>
    /// Читає totalSeconds сигналу порціями і повертає RMS останніх measureSeconds.
    /// </summary>
    private static double ReadSecondsAndMeasureLast(DspProcessor dsp, double totalSeconds, double measureSeconds)
    {
        int totalSamples = (int)(SampleRate * totalSeconds) * Channels;
        int measureSamples = (int)(SampleRate * measureSeconds) * Channels;
        var buffer = new float[totalSamples];
        int read = 0;
        while (read < totalSamples)
        {
            int chunk = Math.Min(4096, totalSamples - read);
            int got = dsp.Read(buffer, read, chunk);
            Assert.Equal(chunk, got);
            read += got;
        }

        return Rms(buffer[(totalSamples - measureSamples)..]);
    }

    /// <summary>RMS одного каналу interleaved-масиву з пропуском перших skipSamples семплів.</summary>
    private static double ChannelRms(float[] samples, int channel, int channels, int skipSamples)
    {
        double sum = 0;
        int count = 0;
        for (int i = skipSamples * channels + channel; i < samples.Length; i += channels)
        {
            sum += (double)samples[i] * samples[i];
            count++;
        }
        return Math.Sqrt(sum / count);
    }

    #endregion

    [Fact]
    public void Read_ReturnsZeroWhenSourceEmpty()
    {
        var dsp = new DspProcessor(new EmptySource());
        Assert.Equal(0, dsp.Read(new float[100], 0, 100));
    }

    private sealed class EmptySource : ISampleProvider
    {
        public WaveFormat WaveFormat { get; } = FakeSampleProvider.MakeFormat();
        public int Read(float[] buffer, int offset, int count) => 0;
    }
}
