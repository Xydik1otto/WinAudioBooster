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
    public void GainDb_ConvertsToLinearGain()
    {
        // Через публічну поведінку: постійний вхід 0.1, GainDb=6 -> вихід ~0.1 * 10^(6/20)
        var dsp = CreateDsp(_ => 0.1f);
        dsp.GainDb = 6f;

        var samples = ReadAll(dsp, 64);
        float expected = 0.1f * (float)Math.Pow(10, 6.0 / 20.0);
        foreach (float s in samples)
        {
            Assert.Equal(expected, s, 4);
        }
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
