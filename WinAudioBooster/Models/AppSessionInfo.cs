namespace WinAudioBooster.Models;

public class AppSessionInfo
{
    public int ProcessId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public double VolumePercent { get; set; } = 100.0; // 0..300%
    public bool IsMuted { get; set; }
}