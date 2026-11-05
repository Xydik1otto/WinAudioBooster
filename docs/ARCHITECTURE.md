# WinAudioBooster Architecture

Technical description of the project internals. User-facing documentation is in [README.md](../README.md).

## Overview

WinAudioBooster is a WPF application (.NET 8, C#) built on **NAudio** (audio processing) and **AudioSwitcher.AudioApi.CoreAudio** (CoreAudio device control). The application intercepts system audio through a virtual audio cable, processes it with a DSP chain, and plays it back on the physical device.

## Audio flow

```
 ┌─────────────────────┐
 │  Windows apps        │  (browser, games, player...)
 │  (system mixer)      │
 └──────────┬──────────┘
            │  Windows switches the default output
            ▼
 ┌─────────────────────┐
 │  VB-Audio CABLE      │  virtual device "CABLE Input"
 │  (default output)    │
 └──────────┬──────────┘
            │  WASAPI loopback capture (WasapiLoopbackCapture)
            ▼
 ┌─────────────────────┐
 │  BufferedWaveProvider│  buffer ~2 s, DiscardOnBufferOverflow
 └──────────┬──────────┘
            ▼
 ┌─────────────────────┐
 │  DspProcessor        │  ISampleProvider, per-sample processing:
 │  1. Gain (to +300%)  │     gain → 10-band EQ → boost shelves →
 │  2. 10-band EQ       │     widen (M/S) → crossfeed →
 │  3. Boost shelves    │     balance/mono → compressor →
 │  4. Stereo widening  │     loudness AGC (RMS) →
 │  5. Crossfeed        │     soft limiter (ceiling)
 │  6. Balance / Mono   │     LevelMeter: input/output levels,
 │  7. Compressor       │     FFT spectrum for the visualizer
 │  8. Loudness AGC     │
 │  9. Soft limiter     │
 └──────────┬──────────┘
            │  resampling to the device format
            ▼
 ┌─────────────────────┐
 │  MediaFoundation     │  (if format != the device MixFormat;
 │  Resampler           │   otherwise direct SampleToWaveProvider16)
 └──────────┬──────────┘
            ▼
 ┌─────────────────────┐
 │  WasapiOut (Shared)  │  output to the physical device
 └──────────┬──────────┘
            ▼
 ┌─────────────────────┐
 │  Speakers / headphones│
 └─────────────────────┘
```

Chain components (the `Audio.AudioEngine` class):

1. **Auto switching** — `Utils.AudioDeviceSwitcher` assigns "CABLE Input" as the default device for the Console/Multimedia/Communications roles through the COM interface `IPolicyConfig`. On stop it restores the previous device.
2. **Capture** — `WasapiLoopbackCapture(captureCableDevice)` takes the loopback stream from the virtual cable.
3. **Buffering** — `BufferedWaveProvider` smooths uneven data arrival (a 2-second buffer; on overflow, older data is discarded).
4. **DSP** — `Audio.DspProcessor` implements `ISampleProvider` and runs every sample through the chain: gain (up to 300%, i.e. above 0 dBFS), equalizer, boost shelves (Bass/Treble Boost), stereo widening (M/S image widening), crossfeed (delay/mixing of the opposite channel for headphones), balance/mono, a dynamic compressor (threshold/ratio/attack/release), loudness normalization (RMS AGC — holds a target level), and a soft limiter (limits peaks to a set ceiling, -0.3 dB by default). Built-in `LevelMeter` instances provide levels for the UI indicators, and the FFT spectrum feeds the spectrum visualizer (with peak history).
5. **Output** — if the format after DSP does not match the physical device's `MixFormat`, `MediaFoundationResampler` runs; then `WasapiOut` in shared mode plays the audio.

## Layers

| Layer / folder | Responsibility |
|---|---|
| `Audio/` | Audio core: engine, DSP, device control, level metering |
| `Services/` | Application services: settings, presets, themes, tray, hotkeys, autostart, the VB-Cable installer, update check/download, file logging, app detector/mixer |
| `Models/` | POCO data models: `AppSettings`, `Preset`, `ProcessRule`, `AppVolumeRule`, `DevicePresetBinding`, `AppSessionInfo` |
| `Utils/` | Helper utilities: device switcher (COM), JSON, culture/language |
| `Converters/` | WPF converters for the UI (color/visibility based on gain level) |
| `MainWindow/` | Main window — a **partial class** split by function |
| `Themes/Skins/` | XAML resource dictionaries for UI themes |
| `Resources/` | Localization (ResourceDictionary `Strings.uk.xaml`, `Strings.en.xaml`) |
| `Deployment/` | Installer (Inno Setup `.iss`) and uninstall script |

### MainWindow as a partial class

The main window is deliberately split into parts by domain so that thousands of lines are not kept in a single file:

| File | Area of responsibility |
|---|---|
| `MainWindow.xaml.cs` | Initialization, lifecycle, basic UI event handlers |
| `MainWindow.Audio.cs` | `AudioEngine` control: booster start/stop, level indicators |
| `MainWindow.Settings.cs` | Settings panel, language, theme, autostart, hotkeys |
| `MainWindow.Presets.cs` | Preset list, creation/rename/deletion |
| `MainWindow.AppMixer.cs` | App mixer: process list, volume rules |
| `MainWindow.Profiles.cs` | Device auto-profiles: "output device → preset" bindings |
| `MainWindow.SleepTimer.cs` | Sleep timer: 15/30/60 minutes, smooth fade-out |
| `MainWindow.Spectrum.cs` | Spectrum visualizer: FFT spectrum with peak history |

`UpdateService` (update check and download) is driven from `MainWindow.Settings.cs`, and logging via `LoggingService` from `MainWindow.Settings.cs` / `App.xaml.cs` respectively.

## File tree

```
WinAudioBooster/
├── WinAudioBooster.sln                  # Solution (app + tests)
├── global.json                          # .NET SDK 8 version pin
├── VBCABLE_Driver_Pack45.zip            # VB-Cable driver archive for the built-in installer
├── .github/workflows/
│   ├── build.yml                        # CI: restore/build/test on push and PR
│   └── release.yml                      # Release on v* tag: publish + GitHub Release
├── docs/
│   └── ARCHITECTURE.md                  # This document
├── LICENSE                              # MIT
└── WinAudioBooster/                     # Main project (WPF)
    ├── WinAudioBooster.csproj           # net8.0-windows; single-file, self-contained win-x64
    ├── App.xaml / App.xaml.cs           # Entry point, global resources, error handling
    ├── GlobalUsings.cs                  # Global usings
    ├── MiniMixerWindow.xaml(.cs)        # Mini window (compact mode)
    ├── Audio/
    │   ├── AudioEngine.cs               # Orchestration: capture → buffer → DSP → out
    │   ├── DspProcessor.cs              # Gain, EQ, compressor, limiter (ISampleProvider)
    │   ├── DeviceManager.cs             # Audio device lookup/listing (MMDevice)
    │   ├── AudioDeviceMonitor.cs        # Device connect/disconnect tracking
    │   ├── VolumeManager.cs             # Device volume control
    │   └── LevelMeter.cs                # Level calculation for indicators
    ├── Services/
    │   ├── SettingsManager.cs           # appsettings.json load/save
    │   ├── PresetManager.cs             # Built-in + custom presets (presets.json)
    │   ├── ThemeManager.cs              # Theme application (merged dictionaries)
    │   ├── TrayService.cs               # Notification-area icon and menu
    │   ├── HotkeyManager.cs             # Global hotkeys (RegisterHotKey)
    │   ├── AutoStartService.cs          # Autostart with Windows
    │   ├── VbCableInstaller.cs          # VB-Cable installation (local zip or download)
    │   ├── UpdateService.cs             # Updates via the GitHub Releases API: check + exe download with SHA256 verification, `.bak` backup, automatic restart
    │   ├── LoggingService.cs            # File log in %APPDATA%\WinAudioBooster\log.txt (diagnostics)
    │   ├── AppDetector.cs               # Detection of running applications (audio sessions)
    │   └── AppVolumeService.cs          # Per-application volume (ISimpleAudioVolume)
    ├── Models/
    │   ├── AppSettings.cs               # Application state (gain, compressor, limiter, theme, language, hotkey)
    │   ├── Preset.cs                    # Preset: gain, compressor threshold/ratio, limiter ceiling
    │   ├── ProcessRule.cs               # Volume rule for a process (app mixer)
    │   ├── AppVolumeRule.cs             # Persistent per-app volume rule (kept between sessions)
    │   ├── DevicePresetBinding.cs       # Auto-profile: "output device → preset" binding
    │   └── AppSessionInfo.cs            # Information about an application's audio session
    ├── Utils/
    │   ├── AudioDeviceSwitcher.cs       # IPolicyConfig: set/restore default device
    │   ├── JsonHelper.cs                # JSON file read/write
    │   └── CultureHelper.cs             # Interface language switching (uk/en)
    ├── Converters/
    │   ├── GainToColorConverter.cs      # Indicator color depending on gain
    │   └── GainToWarningVisibilityConverter.cs # Warning at dangerous gain
    ├── MainWindow/
    │   ├── MainWindow.xaml              # Main window markup
    │   └── MainWindow.*.cs              # Partial classes (see the table above)
    ├── Themes/Skins/
    │   ├── Win11Fluent.xaml             # "Windows 11" theme
    │   ├── ArchLinux.xaml               # "Arch Linux" theme
    │   ├── WindowsXP.xaml               # "Windows XP" theme
    │   ├── StudioRack.xaml              # "StudioRack" theme (default)
    │   └── LightOverlay.xaml            # Light overlay for the "Auto (Windows)" mode
    ├── Resources/
    │   ├── Strings.uk.xaml              # Ukrainian localization
    │   └── Strings.en.xaml              # English localization
    └── Deployment/
        ├── WinAudioBooster-Setup.iss    # Installer script (Inno Setup)
        └── Uninstall.ps1                # Uninstall script
```

## Settings data flow

Configuration files are stored in **`%APPDATA%\WinAudioBooster\`** (not in the registry). Migration: if a file exists next to the exe (`AppDomain.CurrentDomain.BaseDirectory` — the old portable scheme) but not yet in APPDATA, it is copied there automatically on first launch.

```
┌──────────────┐   LoadSettings()    ┌──────────────┐
│ appsettings. │ ──────────────────► │  Settings    │ ──► AppSettings (Models)
│ json         │ ◄────────────────── │  Manager     │     (gain, EQ, compressor, limiter,
└──────────────┘   SaveSettings()    └──────────────┘      theme, language, hotkey, device,
                                                            app mixer rules)
┌──────────────┐   LoadCustomPresets() ┌──────────────┐
│ presets.json │ ────────────────────► │   Preset     │ ──► List<Preset>
│ (custom      │ ◄──────────────────── │   Manager    │     built-in presets are in code
│  presets)    │   SaveCustomPreset()  └──────────────┘
└──────────────┘
```

- `appsettings.json` — serialization of `Models.AppSettings` via `System.Text.Json` (indented). If the file is missing or corrupted, defaults are applied.
- `presets.json` — an array of `Models.Preset` (custom only; the five built-in presets are generated by `PresetManager.GetBuiltInPresets()`). Saving rewrites the whole file.
- The UI reads/writes settings only through the managers; changes are applied to `DspProcessor` on the fly (without restarting the audio chain).

## Extensions

### How to add a built-in preset

In `Services/PresetManager.cs`, add an entry to the `GetBuiltInPresets()` list:

```csharp
new() {
    Name = "Night Mode",
    IsBuiltIn = true,
    GainDb = 110,
    CompressorThresholdDb = -24,
    CompressorRatio = 3.5f,
    LimiterCeilingDb = -0.4f,
    EqGainsDb = new double[] { 0, 0, 0, 0, 0, 0, 2, 2, 0, 0 } // optional, 10 bands
}
```

The fields correspond to `Models/Preset.cs`; `IsBuiltIn = true` means the preset is not written to `presets.json` and cannot be deleted from the UI.

### How to add an EQ band

1. In `Audio/DspProcessor.cs`, add a frequency to the static `BandFrequencies` array — `BiQuadFilter.PeakingEQ` filters are created automatically for each band and channel.
2. The gain range is defined by the `MinBandGainDb` / `MaxBandGainDb` constants (currently -12…+12 dB).
3. In `MainWindow/MainWindow.xaml`, add a slider to the `UniformGrid` in the "Equalizer" section (the neighboring 10 sliders are the reference; the band frequency is passed via `Tag`).
4. If needed, add band values to a preset (`Models/Preset.cs`, the `EqGainsDb` array).

### How to add a theme

1. Create an XAML dictionary in `Themes/Skins/`, for example `Themes/Skins/MyTheme.xaml`, with color/style definitions (`StudioRack.xaml` is the reference).
2. Register the name in `Services/ThemeManager.cs` in the `ApplyTheme` switch expression:

```csharp
"MyTheme" => "Themes/Skins/MyTheme.xaml",
```

3. Add an entry to the theme list in the settings UI (`MainWindow.Settings.cs`) so the theme can be selected.
4. Add translations of the theme name in `Resources/Strings.uk.xaml` / `Strings.en.xaml` if the name is localized.

### How to add a language

1. Create `Resources/Strings.<code>.xaml` following the `Strings.uk.xaml` pattern.
2. Add the culture to `Utils/CultureHelper.cs`.
3. Add an option to the language selector in the settings UI.
