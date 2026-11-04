using WinAudioBooster.Audio;

namespace WinAudioBooster.Models;

public class Preset
{
    public string Name { get; set; } = string.Empty;
    public bool IsBuiltIn { get; set; } = false;

    public double GainDb { get; set; } = 3.0;
    public double CompressorThresholdDb { get; set; } = -20.0;
    public double CompressorRatio { get; set; } = 3.0;
    public double CompressorAttackMs { get; set; } = 10.0;
    public double CompressorReleaseMs { get; set; } = 100.0;
    public double LimiterThresholdDb { get; set; } = -1.0;
    public double LimiterCeilingDb { get; set; } = -0.3;

    // 10-смуговий еквалайзер (гейни смуг у дБ, за замовчуванням — 0)
    public double[] EqGainsDb { get; set; } = new double[DspProcessor.BandCount];

    /// <summary>Копіює EQ-гейни з іншого пресету (захист від спільного масиву).</summary>
    public void CopyEqGainsFrom(Preset other)
    {
        if (other?.EqGainsDb == null)
        {
            EqGainsDb = new double[DspProcessor.BandCount];
            return;
        }

        var copy = new double[DspProcessor.BandCount];
        for (int i = 0; i < copy.Length; i++)
        {
            copy[i] = (i < other.EqGainsDb.Length) ? other.EqGainsDb[i] : 0.0;
        }
        EqGainsDb = copy;
    }

    public override string ToString() => Name;
}