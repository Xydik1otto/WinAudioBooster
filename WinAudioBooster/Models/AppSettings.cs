namespace WinAudioBooster.Models
{
    public class AppSettings
    {
        public bool IsBoosterEnabled { get; set; } = false;
        public string Language { get; set; } = "uk-UA";
        public string SelectedPresetName { get; set; } = "Light Boost";
        
        public double GainDb { get; set; } = 3.0;
        public double CompressorThresholdDb { get; set; } = -20.0;
        public double CompressorRatio { get; set; } = 3.0;
        public double LimiterCeilingDb { get; set; } = -0.3;
    }
}