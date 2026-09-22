using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WinAudioBooster.Models;
using WinAudioBooster.Utils;

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
                new Preset { Name = "Light Boost", IsBuiltIn = true, GainDb = 120, CompressorThresholdDb = -20, CompressorRatio = 2.0f, LimiterCeilingDb = -0.3f },
                new Preset { Name = "Max Boost", IsBuiltIn = true, GainDb = 200, CompressorThresholdDb = -30, CompressorRatio = 4.0f, LimiterCeilingDb = -0.3f },
                new Preset { Name = "Headphones", IsBuiltIn = true, GainDb = 140, CompressorThresholdDb = -25, CompressorRatio = 3.0f, LimiterCeilingDb = -0.5f },
                new Preset { Name = "Games", IsBuiltIn = true, GainDb = 160, CompressorThresholdDb = -28, CompressorRatio = 5.0f, LimiterCeilingDb = -0.2f },
                new Preset { Name = "Calls", IsBuiltIn = true, GainDb = 125, CompressorThresholdDb = -18, CompressorRatio = 4.0f, LimiterCeilingDb = -0.5f }
            };
        }

        public List<Preset> LoadCustomPresets()
        {
            return JsonHelper.ReadJson<List<Preset>>(_presetsFilePath) ?? new List<Preset>();
        }

        public List<Preset> LoadAllPresets()
        {
            var all = GetBuiltInPresets();
            var custom = LoadCustomPresets();
            all.AddRange(custom);
            return all;
        }

        public void SaveCustomPreset(Preset newPreset)
        {
            var custom = LoadCustomPresets();
            var existing = custom.FirstOrDefault(p => p.Name.Equals(newPreset.Name, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                existing.GainDb = newPreset.GainDb;
                existing.CompressorThresholdDb = newPreset.CompressorThresholdDb;
                existing.CompressorRatio = newPreset.CompressorRatio;
                existing.LimiterCeilingDb = newPreset.LimiterCeilingDb;
            }
            else
            {
                newPreset.IsBuiltIn = false;
                custom.Add(newPreset);
            }

            JsonHelper.WriteJson(_presetsFilePath, custom);
        }

        public void RenameCustomPreset(string oldName, string newName)
        {
            var custom = LoadCustomPresets();
            var target = custom.FirstOrDefault(p => p.Name.Equals(oldName, StringComparison.OrdinalIgnoreCase));

            if (target != null)
            {
                target.Name = newName;
                JsonHelper.WriteJson(_presetsFilePath, custom);
            }
        }

        public void DeleteCustomPreset(string presetName)
        {
            var custom = LoadCustomPresets();
            custom.RemoveAll(p => p.Name.Equals(presetName, StringComparison.OrdinalIgnoreCase));
            JsonHelper.WriteJson(_presetsFilePath, custom);
        }
    }
}