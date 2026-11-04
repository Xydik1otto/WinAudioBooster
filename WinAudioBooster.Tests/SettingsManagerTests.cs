using System.IO;
using WinAudioBooster.Models;
using WinAudioBooster.Services;
using Xunit;

namespace WinAudioBooster.Tests;

public class SettingsManagerTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _appDataDir;
    private readonly string _legacyBaseDir;

    public SettingsManagerTests()
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

    [Fact]
    public void Roundtrip_PreservesAllFieldsIncludingEq()
    {
        var manager = new SettingsManager(_appDataDir);
        var settings = new AppSettings
        {
            IsBoosterEnabled = true,
            Language = "uk",
            GainDb = 123.4,
            CompressorThresholdDb = -18.0,
            CompressorRatio = 4.5,
            LimiterCeilingDb = -1.5,
            IsEqEnabled = false,
            EqGainsDb = new double[] { 1, 2, 3, 4, 5, -5, -4, -3, -2, -1 },
            SelectedTheme = "StudioRack"
        };

        manager.SaveSettings(settings);

        var loaded = new SettingsManager(_appDataDir).LoadSettings();

        Assert.True(loaded.IsBoosterEnabled);
        Assert.Equal("uk", loaded.Language);
        Assert.Equal(123.4, loaded.GainDb, 5);
        Assert.Equal(4.5, loaded.CompressorRatio, 5);
        Assert.Equal(-1.5, loaded.LimiterCeilingDb, 5);
        Assert.False(loaded.IsEqEnabled);
        Assert.NotNull(loaded.EqGainsDb);
        Assert.Equal(10, loaded.EqGainsDb.Length);
        Assert.Equal(new double[] { 1, 2, 3, 4, 5, -5, -4, -3, -2, -1 }, loaded.EqGainsDb);
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var manager = new SettingsManager(_appDataDir);
        var settings = manager.LoadSettings();

        Assert.True(settings.IsEqEnabled);
        Assert.NotNull(settings.EqGainsDb);
        Assert.Equal(10, settings.EqGainsDb.Length);
        Assert.All(settings.EqGainsDb, g => Assert.Equal(0.0, g));
    }

    [Fact]
    public void Migration_CopiesFromLegacyBaseDirectory_WhenAppDataEmpty()
    {
        // Готуємо "старі" налаштування у BaseDirectory
        var legacySettings = new AppSettings { Language = "en", GainDb = 55.5 };
        string legacyJson = System.Text.Json.JsonSerializer.Serialize(legacySettings);
        File.WriteAllText(Path.Combine(_legacyBaseDir, "appsettings.json"), legacyJson);

        var manager = new SettingsManager(_appDataDir, _legacyBaseDir);
        var loaded = manager.LoadSettings();

        Assert.Equal("en", loaded.Language);
        Assert.Equal(55.5, loaded.GainDb, 5);
        // Файл мігрував у APPDATA-папку
        Assert.True(File.Exists(Path.Combine(_appDataDir, "appsettings.json")));
    }

    [Fact]
    public void Migration_DoesNotOverwriteExistingAppDataFile()
    {
        var appDataManager = new SettingsManager(_appDataDir);
        appDataManager.SaveSettings(new AppSettings { Language = "uk" });

        string legacyJson = System.Text.Json.JsonSerializer.Serialize(new AppSettings { Language = "en" });
        File.WriteAllText(Path.Combine(_legacyBaseDir, "appsettings.json"), legacyJson);

        var manager = new SettingsManager(_appDataDir, _legacyBaseDir);
        Assert.Equal("uk", manager.LoadSettings().Language);
    }

    [Fact]
    public void Migration_NoLegacyFile_WorksFine()
    {
        var manager = new SettingsManager(_appDataDir, _legacyBaseDir);
        Assert.True(manager.LoadSettings().IsEqEnabled);
    }
}
