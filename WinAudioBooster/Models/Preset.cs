namespace WinAudioBooster.Models
{
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

        public override string ToString() => Name;
    }
}