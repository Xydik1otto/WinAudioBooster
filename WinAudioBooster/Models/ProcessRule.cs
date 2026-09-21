namespace WinAudioBooster.Models
{
    public class ProcessRule
    {
        public string ProcessName { get; set; } = string.Empty;
        public string TargetPresetName { get; set; } = string.Empty;
        public int Priority { get; set; } = 1;
    }
}