namespace WinAudioBooster.Models;

/// <summary>Запам'ятована гучність процесу (per-app), 0..300%.</summary>
public class AppVolumeRule
{
    public string ProcessName { get; set; } = string.Empty;
    public double VolumePercent { get; set; } = 100.0;
}
