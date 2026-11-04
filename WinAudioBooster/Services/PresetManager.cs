using WinAudioBooster.Audio;

namespace WinAudioBooster.Services;

public class PresetManager
{
    private readonly string _presetsFilePath;
    private readonly string? _legacyPresetsFilePath;

    public PresetManager()
    {
        // Пресети зберігаємо у %APPDATA%\WinAudioBooster, а не поруч із exe (BaseDirectory)
        _presetsFilePath = Path.Combine(SettingsManager.GetDefaultAppDataDirectory(), "presets.json");
        _legacyPresetsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "presets.json");
        MigrateFromLegacyIfNeeded();
    }

    /// <summary>
    /// Конструктор для тестів: дозволяє підмінити APPDATA-папку та стару BaseDirectory-папку.
    /// </summary>
    public PresetManager(string appDataDirectory, string? legacyBaseDirectory = null)
    {
        _presetsFilePath = Path.Combine(appDataDirectory, "presets.json");
        _legacyPresetsFilePath = legacyBaseDirectory == null
            ? null
            : Path.Combine(legacyBaseDirectory, "presets.json");
        MigrateFromLegacyIfNeeded();
    }

    /// <summary>Якщо пресети є у старій BaseDirectory, а в APPDATA немає — переносимо.</summary>
    private void MigrateFromLegacyIfNeeded()
    {
        try
        {
            if (_legacyPresetsFilePath == null) return;
            if (!File.Exists(_legacyPresetsFilePath)) return;
            if (File.Exists(_presetsFilePath)) return;

            Directory.CreateDirectory(Path.GetDirectoryName(_presetsFilePath)!);
            File.Copy(_legacyPresetsFilePath, _presetsFilePath, overwrite: false);
        }
        catch
        {
            // Міграція не критична: користувач просто не побачить старі кастомні пресети
        }
    }

    public List<Preset> GetBuiltInPresets()
    {
        // Вбудовані EQ-криві: Headphones — легкий V, Calls — підйом 1-4 кГц,
        // Games — підйон низів і високих, Light/Max Boost — flat.
        var vShape = new double[] { 2.0, 1.5, 1.0, 0.0, -1.0, -1.0, -0.5, 1.0, 2.0, 3.0 };
        var callsShape = new double[] { 0.0, 0.0, 0.0, 0.0, 0.0, 2.0, 4.0, 4.0, 1.0, 0.0 };
        var gamesShape = new double[] { 4.0, 3.0, 2.0, 0.0, 0.0, 0.0, 1.0, 2.0, 3.0, 4.0 };
        var flat = new double[DspProcessor.BandCount];

        var presets = new List<Preset>
        {
            new() { Name = "Light Boost", IsBuiltIn = true, GainDb = 120, CompressorThresholdDb = -20, CompressorRatio = 2.0f, LimiterCeilingDb = -0.3f, EqGainsDb = (double[])flat.Clone() },
            new() { Name = "Max Boost", IsBuiltIn = true, GainDb = 200, CompressorThresholdDb = -30, CompressorRatio = 4.0f, LimiterCeilingDb = -0.3f, EqGainsDb = (double[])flat.Clone() },
            new() { Name = "Headphones", IsBuiltIn = true, GainDb = 140, CompressorThresholdDb = -25, CompressorRatio = 3.0f, LimiterCeilingDb = -0.5f, EqGainsDb = (double[])vShape.Clone() },
            new() { Name = "Games", IsBuiltIn = true, GainDb = 160, CompressorThresholdDb = -28, CompressorRatio = 5.0f, LimiterCeilingDb = -0.2f, EqGainsDb = (double[])gamesShape.Clone() },
            new() { Name = "Calls", IsBuiltIn = true, GainDb = 125, CompressorThresholdDb = -18, CompressorRatio = 4.0f, LimiterCeilingDb = -0.5f, EqGainsDb = (double[])callsShape.Clone() }
        };

        foreach (var p in presets) NormalizePreset(p);
        return presets;
    }

    public List<Preset> LoadCustomPresets()
    {
        var presets = JsonHelper.ReadJson<List<Preset>>(_presetsFilePath) ?? new List<Preset>();
        foreach (var p in presets) NormalizePreset(p);
        return presets;
    }

    public List<Preset> LoadAllPresets()
    {
        var all = GetBuiltInPresets();
        all.AddRange(LoadCustomPresets());
        return all;
    }

    /// <summary>Гарантує, що EqGainsDb має 10 елементів (старі JSON-файли можуть його не містити).</summary>
    private static void NormalizePreset(Preset preset)
    {
        if (preset.EqGainsDb == null || preset.EqGainsDb.Length != DspProcessor.BandCount)
        {
            var normalized = new double[DspProcessor.BandCount];
            if (preset.EqGainsDb != null)
            {
                for (int i = 0; i < Math.Min(normalized.Length, preset.EqGainsDb.Length); i++)
                {
                    normalized[i] = preset.EqGainsDb[i];
                }
            }
            preset.EqGainsDb = normalized;
        }
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
            existing.CopyEqGainsFrom(newPreset);
        }
        else
        {
            newPreset.IsBuiltIn = false;
            NormalizePreset(newPreset);
            newPreset.EqGainsDb = (double[])(newPreset.EqGainsDb.Clone());
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
