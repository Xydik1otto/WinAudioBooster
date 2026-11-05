# Changelog

All notable changes to this project are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and versioning follows [Semantic Versioning](https://semver.org/).

## [1.4.0] - 2026-11-05

### Added

- **MVVM refactoring** — `CommunityToolkit.Mvvm`: `MainViewModel`/`EqViewModel` as the source of truth for state, about 20 controls moved to TwoWay bindings, save/restore via `Snapshot()`/`Apply()`; behavior unchanged.
- **APO integration starter package** (`native/WinAudioApo/`, `docs/apo/`, `packaging/apo/`) — the path toward fully removing VB-Cable and zero latency: a C++/COM APO skeleton (gain → 10-band EQ → tanh limiter, config from `%APPDATA%\WinAudioBooster\apo-config.ini`), a migration and distribution plan following the Equalizer APO pattern; the WDK is required to build it.
- **Security fixes from the review** (`docs/security-review-2026-11.md`): auto-update is now fail-closed (without a SHA256 from GitHub, installation is refused); the VB-Cable installer accepts only its own downloaded zip in `%TEMP%` plus a mandatory SHA256 check before the elevated launch; PowerShell injection through the device name is eliminated.
- **Quality infrastructure**: `.editorconfig` (conservative style plus targeted CA suppressions with explanations), `.github/dependabot.yml` (nuget + actions, weekly), .NET analyzers `latest-recommended` with `EnforceCodeStyleInBuild`.
- **Distribution size: 154 MB → 131 MB (-15%)** — `SatelliteResourceLanguages=en` removed satellite framework assemblies; trimming for WPF is not possible (NETSDK1168), documented.
- **Six professional presets** — Movies, Music, Vocal Boost, Bass Head, Late Night Dialog, Flat/Reference — with meaningful EQ curves, joining the existing six (12 built-in presets in total).
- **Custom color palettes** — "Create from current" captures the live theme colors (including the auto Windows-theme overlay), custom palettes are stored in `appsettings.json`, selectable and deletable from the theme section, restored on startup.
- **UI text cleanup** — hyperbolic or wordy strings rewritten short and factual; all hardcoded strings in the main window and the mini mixer moved to localization resources (uk/en, 102 keys each).
- **Documentation in English** — README, CHANGELOG, ARCHITECTURE, packaging and APO guides rewritten in plain English without emoji; renamed `README-uk.md` files to `README.md`.

## [1.3.0] - 2026-11-05

### Added

- **Real FFT spectrum analyzer** — 32 logarithmic bands (20 Hz…20 kHz), Hann window, smooth falloff; replaced the peak history in the main window and the mini mixer (idea from FxSound).
- **AutoEq curve import** — load CSV/TXT files from the [AutoEq](https://github.com/jaakkopasanen/AutoEq) database (thousands of measured headphone corrections) — the curve is interpolated onto the 10 EQ bands (idea from Peace).
- Spectral tap in the DSP: a 4096-sample ring buffer of mono samples after the limiter, thread-safe, switchable to save CPU; the old double tap from 1.2.0 removed.

## [1.2.0] - 2026-11-05

### Added

- **Loudness normalization** — level alignment to a target level (RMS AGC): quiet sources are raised, loud ones do not clip.
- **Crossfeed** — a "speakers through headphones" effect (delay/mixing of the opposite channel).
- **Stereo widening** — widening/narrowing of the stereo image (M/S processing).
- **Mono / Balance** — mono switching and L/R balance control.
- **Balance / Mono / Normalization checkboxes** — quick control of the new modes in the main window.
- **Spectrum visualizer** — real-time spectrum visualization with peak history.
- **OSD feedback** — on-screen hints when changing volume/modes.
- **Quiet hours** — automatic attenuation at a scheduled time.
- **One-click auto-update** — download and install a new version from GitHub Releases with SHA256 verification, a `.bak` backup, and automatic restart (`Services/UpdateService.cs`).
- **File logging** — diagnostics in `%APPDATA%\WinAudioBooster\log.txt` (`Services/LoggingService.cs`).
- **CI auto-hashing of manifests** — the `update-manifests` job in release.yml updates the SHA256 values in the winget and Scoop manifests right after a release is published.

## [1.1.0] - Unreleased

### Added

- **Latency reduction**: audio buffer 2 s → 250 ms, WasapiOut 50 ms → 30 ms.
- **Smooth gain** (slew limiting) — moving the volume slider no longer clicks.
- **Device auto-profiles** — "output device to preset" bindings: connecting headphones activates the headphone profile.
- **Persistent per-app volume** — the mixer remembers configured application levels between sessions.
- **Hearing protection** — a warning when boosting above 150% (once per session, can be disabled in settings).
- **Bass Boost / Treble Boost / Cut sub-bass (30 Hz)** — one-click toggles on top of the EQ (idea from FxSound).
- **Night Mode preset** — a gentle compressor with a -6 dB ceiling (idea from Sound Lock / Windows Loudness Equalization).
- **Auto theme following Windows** — the "Auto (Windows)" mode follows the system theme (EarTrumpet pattern) plus a light overlay.
- **New hotkeys**: Ctrl+Alt+1..5 → presets 1-5, Ctrl+Alt+D → DSP bypass (idea from Peace/FxSound).
- **Sleep timer** — shutdown after 15/30/60 minutes with a smooth 10-second fade-out (idea from Volume²).
- **Open-source infrastructure**: CONTRIBUTING.md, SECURITY.md, Issue/PR templates, CHANGELOG, README badges, winget and Scoop manifests, Inno Setup installer build in CI.

## [1.0.1] - 2026-11-05

### Fixed

- Fixed the test project build and CI workflow stability (`build.yml`).
- Clarified **limiter** behavior (a correct -0.3 dB ceiling without residual clipping).

### Added

- **Unit test coverage** (`WinAudioBooster.Tests`).

## [1.0.0] - 2026-11-05

### Added

- Volume booster up to 300% via software DSP gain.
- 10-band equalizer with presets (Light Boost, Max Boost, Headphones, Games, Calls) and custom presets.
- Compressor (threshold, ratio, attack/release) and a soft limiter.
- Per-application volume mixer.
- Mini window, tray control, global hotkeys (`Ctrl + Alt + B`), autostart.
- 4 themes (Win11 Fluent, Arch Linux, Windows XP, StudioRack) and UA/EN localization.
- Automatic switching of the system output to VB-Cable and back.
- Update checks via GitHub Releases; single-file self-contained build in CI.

[Unreleased]: https://github.com/Xydik1otto/WinAudioBooster/compare/v1.0.1...HEAD
[1.1.0]: https://github.com/Xydik1otto/WinAudioBooster/compare/v1.0.1...v1.1.0
[1.0.1]: https://github.com/Xydik1otto/WinAudioBooster/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/Xydik1otto/WinAudioBooster/releases/tag/v1.0.0
