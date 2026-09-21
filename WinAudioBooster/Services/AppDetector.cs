using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WinAudioBooster.Models;

namespace WinAudioBooster.Services
{
    public class AppDetector : IDisposable
    {
        private readonly Timer _timer;
        private readonly List<ProcessRule> _defaultRules;
        private List<ProcessRule> _userRules = new();
        private string? _lastDetectedPreset;

        public event EventHandler<string>? PresetChangeRequested;

        public bool IsEnabled { get; set; } = true;

        public AppDetector()
        {
            // Стандартні правила із секції 6.3 Генерального плану
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

            // Сканування кожні 1.5 секунди
            _timer = new Timer(ScanProcesses, null, TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(1500));
        }

        public void UpdateCustomRules(List<ProcessRule>? customRules)
        {
            _userRules = customRules ?? new List<ProcessRule>();
        }

        private void ScanProcesses(object? state)
        {
            if (!IsEnabled) return;

            try
            {
                var activeProcessNames = Process.GetProcesses()
                    .Select(p => p.ProcessName.ToLower() + ".exe")
                    .ToHashSet();

                var allRules = _userRules.Concat(_defaultRules)
                    .OrderBy(r => r.Priority);

                foreach (var rule in allRules)
                {
                    string targetProc = rule.ProcessName.ToLower();
                    if (!targetProc.EndsWith(".exe")) targetProc += ".exe";

                    if (activeProcessNames.Contains(targetProc))
                    {
                        if (_lastDetectedPreset != rule.TargetPresetName)
                        {
                            _lastDetectedPreset = rule.TargetPresetName;
                            PresetChangeRequested?.Invoke(this, rule.TargetPresetName);
                        }
                        return;
                    }
                }

                _lastDetectedPreset = null;
            }
            catch { }
        }

        public void Dispose()
        {
            _timer.Dispose();
        }
    }
}