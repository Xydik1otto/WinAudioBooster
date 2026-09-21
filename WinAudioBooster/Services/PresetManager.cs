using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using WinAudioBooster.Models;

namespace WinAudioBooster.Services
{
    public class PresetManager
    {
        private readonly string _presetsFilePath;

        public PresetManager()
        {
            _presetsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "presets.json");
        }

        public List<Preset> GetBuiltInPresets()
        {
            return new List<Preset>
            {
                new Preset
                {
                    Name = "Light Boost",
                    IsBuiltIn = true,
                    GainDb = 3.0,
                    CompressorThresholdDb = -20.0,
                    CompressorRatio = 2.0,
                    CompressorAttackMs = 10.0,
                    CompressorReleaseMs = 100.0,
                    LimiterThresholdDb = -1.0,
                    LimiterCeilingDb = -0.3
                },
                new Preset
                {
                    Name = "Max Boost",
                    IsBuiltIn = true,
                    GainDb = 10.0,
                    CompressorThresholdDb = -30.0,
                    CompressorRatio = 4.0,
                    CompressorAttackMs = 5.0,
                    CompressorReleaseMs = 150.0,
                    LimiterThresholdDb = -0.5,
                    LimiterCeilingDb = -0.3
                },
                new Preset
                {
                    Name = "Headphones",
                    IsBuiltIn = true,
                    GainDb = 6.0,
                    CompressorThresholdDb = -25.0,
                    CompressorRatio = 3.0,
                    CompressorAttackMs = 8.0,
                    CompressorReleaseMs = 120.0,
                    LimiterThresholdDb = -1.0,
                    LimiterCeilingDb = -0.5
                },
                new Preset
                {
                    Name = "Games",
                    IsBuiltIn = true,
                    GainDb = 7.0,
                    CompressorThresholdDb = -28.0,
                    CompressorRatio = 5.0,
                    CompressorAttackMs = 2.0,
                    CompressorReleaseMs = 80.0,
                    LimiterThresholdDb = -0.5,
                    LimiterCeilingDb = -0.2
                },
                new Preset
                {
                    Name = "Calls",
                    IsBuiltIn = true,
                    GainDb = 5.0,
                    CompressorThresholdDb = -18.0,
                    CompressorRatio = 4.0,
                    CompressorAttackMs = 12.0,
                    CompressorReleaseMs = 200.0,
                    LimiterThresholdDb = -1.5,
                    LimiterCeilingDb = -0.5
                }
            };
        }

        public List<Preset> LoadAllPresets()
        {
            var presets = GetBuiltInPresets();
            var customPresets = LoadCustomPresets();

            foreach (var custom in customPresets)
            {
                if (!presets.Any(p => p.Name.Equals(custom.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    presets.Add(custom);
                }
            }

            return presets;
        }

        public List<Preset> LoadCustomPresets()
        {
            if (!File.Exists(_presetsFilePath)) return new List<Preset>();

            try
            {
                string json = File.ReadAllText(_presetsFilePath);
                var presets = JsonSerializer.Deserialize<List<Preset>>(json);
                return presets ?? new List<Preset>();
            }
            catch
            {
                return new List<Preset>();
            }
        }

        public void SaveCustomPreset(Preset newPreset)
        {
            newPreset.IsBuiltIn = false;
            var customPresets = LoadCustomPresets();

            var existing = customPresets.FirstOrDefault(p => p.Name.Equals(newPreset.Name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                customPresets.Remove(existing);
            }

            customPresets.Add(newPreset);
            SaveCustomPresetsToFile(customPresets);
        }

        public bool DeleteCustomPreset(string presetName)
        {
            var customPresets = LoadCustomPresets();
            var existing = customPresets.FirstOrDefault(p => p.Name.Equals(presetName, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                customPresets.Remove(existing);
                SaveCustomPresetsToFile(customPresets);
                return true;
            }

            return false;
        }

        private void SaveCustomPresetsToFile(List<Preset> presets)
        {
            try
            {
                string json = JsonSerializer.Serialize(presets, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_presetsFilePath, json);
            }
            catch { }
        }
    }
}