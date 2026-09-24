namespace WinAudioBooster.Models
{
    public class ProcessRule
    {
        public string ProcessName { get; set; } = string.Empty;
        public string TargetPresetName { get; set; } = string.Empty; // Необов'язково
        public double VolumePercent { get; set; } = 100.0;           // Індивідуальний буст (0..300%)
        public int Priority { get; set; } = 1;
    }
}