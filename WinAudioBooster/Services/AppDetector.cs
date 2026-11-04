namespace WinAudioBooster.Services;

public class AppDetector : IDisposable
{
    private readonly AppVolumeService _appVolumeService = new();
    private readonly Timer _timer;
    private readonly List<ProcessRule> _defaultRules;
    private List<ProcessRule> _userRules = new();
    private string? _lastDetectedPreset;

    #pragma warning disable CS0067
    public event EventHandler<ProcessRule>? ProcessVolumeApplyRequested;
    #pragma warning restore CS0067

    public event EventHandler<string>? PresetChangeRequested;

    public bool IsEnabled { get; set; } = true;

    public AppDetector()
    {
        _defaultRules = new List<ProcessRule>
        {
            new() { ProcessName = "discord", TargetPresetName = "Calls", Priority = 1 },
            new() { ProcessName = "teams", TargetPresetName = "Calls", Priority = 1 },
            new() { ProcessName = "zoom", TargetPresetName = "Calls", Priority = 1 },
            new() { ProcessName = "cs2", TargetPresetName = "Games", Priority = 2 },
            new() { ProcessName = "cyberpunk2077", TargetPresetName = "Games", Priority = 2 },
            new() { ProcessName = "dota2", TargetPresetName = "Games", Priority = 2 },
            new() { ProcessName = "spotify", TargetPresetName = "Headphones", Priority = 3 },
            new() { ProcessName = "foobar2000", TargetPresetName = "Headphones", Priority = 3 }
        };

        _timer = new Timer(ScanProcesses, null, TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(1500));
    }

    public void UpdateCustomRules(List<ProcessRule>? customRules)
    {
        _userRules = customRules ?? new List<ProcessRule>();
    }

    public static List<string> GetRunningAppsWithWindows()
    {
        try
        {
            return Process.GetProcesses()
                .Where(p => !string.IsNullOrEmpty(p.MainWindowTitle))
                .Select(p => p.ProcessName)
                .Distinct()
                .OrderBy(name => name)
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    private void ScanProcesses(object? state)
    {
        if (!IsEnabled) return;

        try
        {
            var activeProcessNames = Process.GetProcesses()
                .Select(p => p.ProcessName.ToLower() + ".exe")
                .ToHashSet();

            var allRules = _userRules.Concat(_defaultRules).OrderBy(r => r.Priority);

            foreach (var rule in allRules)
            {
                string targetProc = rule.ProcessName.ToLower();
                if (!targetProc.EndsWith(".exe")) targetProc += ".exe";

                if (activeProcessNames.Contains(targetProc))
                {
                    if (_lastDetectedPreset != rule.TargetPresetName && !string.IsNullOrEmpty(rule.TargetPresetName))
                    {
                        _lastDetectedPreset = rule.TargetPresetName;
                        PresetChangeRequested?.Invoke(this, rule.TargetPresetName);
                    }

                    try
                    {
                        var deviceManager = new DeviceManager();
                        var cable = deviceManager.FindCableDevice();
                        _ = _appVolumeService.SetAppVolumeAndBoostAsync(cable, GetProcessIdByName(targetProc), rule.VolumePercent);
                    }
                    catch { }

                    return;
                }
            }

            _lastDetectedPreset = null;
        }
        catch { }
    }

    private int GetProcessIdByName(string procName)
    {
        try
        {
            string cleanName = procName.EndsWith(".exe") ? procName[..^4] : procName;
            var proc = Process.GetProcessesByName(cleanName).FirstOrDefault();
            return proc?.Id ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    public void Dispose() => _timer.Dispose();
}