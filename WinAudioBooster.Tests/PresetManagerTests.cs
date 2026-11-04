using System.IO;
using WinAudioBooster.Models;
using WinAudioBooster.Services;
using Xunit;

namespace WinAudioBooster.Tests;

public class PresetManagerTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _appDataDir;
    private readonly string _legacyBaseDir;

    public PresetManagerTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "wab-tests-" + Guid.NewGuid().ToString("N"));
        _appDataDir = Path.Combine(_tempRoot, "appdata", "WinAudioBooster");
        _legacyBaseDir = Path.Combine(_tempRoot, "basedir");
        Directory.CreateDirectory(_appDataDir);
        Directory.CreateDirectory(_legacyBaseDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    private PresetManager CreateManager() => new(_appDataDir, _legacyBaseDir);

    [Fact]
    public void BuiltInPresets_HaveMeaningfulEqCurves()
    {
        var presets = CreateManager().GetBuiltInPresets();

        Assert.Equal(5, presets.Count);
        foreach (var p in presets)
        {
            Assert.NotNull(p.EqGainsDb);
            Assert.Equal(10, p.EqGainsDb.Length);
            Assert.All(p.EqGainsDb, g => Assert.InRange(g, -12, 12));
        }

        var flat = presets.First(p => p.Name == "Light Boost");
        Assert.All(flat.EqGainsDb, g => Assert.Equal(0.0, g));

        var headphones = presets.First(p => p.Name == "Headphones");
        // V-подібна: низи і верхи підняті, середина знижена
        Assert.True(headphones.EqGainsDb[0] > 0);
        Assert.True(headphones.EqGainsDb[^1] > 0);
        Assert.True(headphones.EqGainsDb[5] < headphones.EqGainsDb[0]);

        var calls = presets.First(p => p.Name == "Calls");
        // Підйом у діапазоні 1-4 кГц (індекси 5..7)
        Assert.True(calls.EqGainsDb[5] > 0 && calls.EqGainsDb[6] > 0 && calls.EqGainsDb[7] > 0);

        var games = presets.First(p => p.Name == "Games");
        Assert.True(games.EqGainsDb[0] > 0 && games.EqGainsDb[^1] > 0);
    }

    [Fact]
    public void SaveCustomPreset_NewPreset_IsPersisted()
    {
        var manager = CreateManager();
        var preset = new Preset
        {
            Name = "My Test",
            GainDb = 150,
            EqGainsDb = new double[] { 1, 2, 3, 4, 5, 5, 4, 3, 2, 1 }
        };

        manager.SaveCustomPreset(preset);

        var custom = new PresetManager(_appDataDir).LoadCustomPresets();
        var saved = custom.FirstOrDefault(p => p.Name == "My Test");
        Assert.NotNull(saved);
        Assert.False(saved!.IsBuiltIn);
        Assert.Equal(150, saved.GainDb);
        Assert.Equal(new double[] { 1, 2, 3, 4, 5, 5, 4, 3, 2, 1 }, saved.EqGainsDb);
    }

    [Fact]
    public void SaveCustomPreset_ExistingName_UpdatesAndCopiesEqGains()
    {
        var manager = CreateManager();
        manager.SaveCustomPreset(new Preset
        {
            Name = "My Test",
            GainDb = 100,
            EqGainsDb = new double[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }
        });

        // Оновлюємо той самий пресет з новими EQ
        manager.SaveCustomPreset(new Preset
        {
            Name = "my test", // регістр не важливий
            GainDb = 180,
            EqGainsDb = new double[] { 5, 4, 3, 2, 1, -1, -2, -3, -4, -5 }
        });

        var custom = manager.LoadCustomPresets();
        var saved = Assert.Single(custom, p => p.Name.Equals("My Test", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(180, saved.GainDb);
        Assert.Equal(new double[] { 5, 4, 3, 2, 1, -1, -2, -3, -4, -5 }, saved.EqGainsDb);
    }

    [Fact]
    public void RenameCustomPreset_RenamesOnlyTarget()
    {
        var manager = CreateManager();
        manager.SaveCustomPreset(new Preset { Name = "A" });
        manager.SaveCustomPreset(new Preset { Name = "B" });

        manager.RenameCustomPreset("A", "A2");

        var names = manager.LoadCustomPresets().Select(p => p.Name).ToList();
        Assert.Contains("A2", names);
        Assert.Contains("B", names);
        Assert.DoesNotContain("A", names);
    }

    [Fact]
    public void DeleteCustomPreset_RemovesTarget()
    {
        var manager = CreateManager();
        manager.SaveCustomPreset(new Preset { Name = "Kill Me" });
        manager.SaveCustomPreset(new Preset { Name = "Keep Me" });

        manager.DeleteCustomPreset("Kill Me");

        var names = manager.LoadCustomPresets().Select(p => p.Name).ToList();
        Assert.Contains("Keep Me", names);
        Assert.DoesNotContain("Kill Me", names);
    }

    [Fact]
    public void LoadCustomPresets_OldJsonWithoutEq_GetsNormalizedToTenZeros()
    {
        // Імітуємо старий JSON без поля EqGainsDb
        string oldJson = """
        [
          { "Name": "Legacy", "IsBuiltIn": false, "GainDb": 100 }
        ]
        """;
        File.WriteAllText(Path.Combine(_appDataDir, "presets.json"), oldJson);

        var manager = CreateManager();
        var custom = manager.LoadCustomPresets();

        var legacy = custom.FirstOrDefault(p => p.Name == "Legacy");
        Assert.NotNull(legacy);
        Assert.NotNull(legacy!.EqGainsDb);
        Assert.Equal(10, legacy.EqGainsDb.Length);
        Assert.All(legacy.EqGainsDb, g => Assert.Equal(0.0, g));
    }

    [Fact]
    public void Migration_CopiesPresetsFromLegacyBaseDirectory()
    {
        string legacyJson = """
        [
          { "Name": "Old Preset", "IsBuiltIn": false, "GainDb": 130 }
        ]
        """;
        File.WriteAllText(Path.Combine(_legacyBaseDir, "presets.json"), legacyJson);

        var manager = new PresetManager(_appDataDir, _legacyBaseDir);
        var custom = manager.LoadCustomPresets();

        Assert.Contains(custom, p => p.Name == "Old Preset");
        Assert.True(File.Exists(Path.Combine(_appDataDir, "presets.json")));
    }

    [Fact]
    public void LoadAllPresets_ContainsBuiltInAndCustom()
    {
        var manager = CreateManager();
        manager.SaveCustomPreset(new Preset { Name = "Custom One" });

        var all = manager.LoadAllPresets();

        Assert.Equal(6, all.Count);
        Assert.Contains(all, p => p.Name == "Custom One" && !p.IsBuiltIn);
        Assert.Contains(all, p => p.Name == "Headphones" && p.IsBuiltIn);
    }
}
