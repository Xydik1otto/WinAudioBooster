# Competitor Research Ideas (Wave 2) — 2026-11-05

Sources: Equalizer APO+Peace, FxSound, Voicemeeter, EarTrumpet, Volume², Sound Lock, EBU R128 normalizers, AutoEq.

## Selected for wave 2 (value × effort on NAudio/WPF)

1. **Bass Boost / Treble toggles (one-click effects, as in FxSound)** — `BiQuadFilter.MakeLowShelf` (~90 Hz, +6..12 dB) / `MakeHighShelf` (~9 kHz) as toggles on top of the EQ. Implementation: two shelf filters per channel in DspProcessor plus `bool IsBassBoostEnabled`/`IsTrebleBoostEnabled`; UI — two checkboxes next to the EQ.
2. **Loudness normalization / night mode (compressor with a ceiling; Sound Lock / Windows Loudness Equalization)** — a compressor and limiter already exist; add a preset/toggle "Night Mode": a gentle threshold, a volume ceiling. Minimal work — a preset plus almost no DSP changes.
3. **Automatic dark/light theme following Windows (the EarTrumpet standard)** — registry `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize` → `AppsUseLightTheme`; subscribe via `SystemEvents.UserPreferenceChanged`; map onto the existing skins (Win11Fluent ↔ a light variant, or light resources).
4. **Global hotkeys for presets/bypass/mute** — `RegisterHotKey` P/Invoke (`HotkeyManager` already exists — extend it): Ctrl+Alt+1..5 → preset, Ctrl+Alt+D → bypass (DSP on/off), feedback via a tray balloon.
5. **Sleep timer + fade-out** — `DispatcherTimer` for N minutes → a smooth gain ramp to 0 → Stop. UI: a ComboBox with 15/30/60 minutes in the system settings.
6. **Spectrum visualizer (the FxSound effect)** — an `ISampleProvider` tap after the DSP → a 2048-sample buffer → `NAudio.Dsp.Fft` → 32 bins → Canvas bars, updated via `CompositionTarget.Rendering`. Medium complexity.
7. **Sub-bass HPF at 30 Hz** (frees headroom when boosting) — `BiQuadFilter.HighPass`, a toggle in the EQ section.

## Deferred (larger / lower priority)

- EBU R128 real-time auto-gain (K-weighted LUFS) — after the MVVM refactoring.
- Crossfeed, stereo widening, AutoEq CSV import, balance/mono, profile scheduler, OSD.

## Direct borrowings from competitors

- FxSound: the A/B bypass toggle (`IsEnabled` already exists — expose it as a hotkey), preset-to-device binding (already in wave 1).
- Peace: preamp compensation when boosting (limit the negative makeup — the limiter already covers it).
- Voicemeeter: a limiter at the end of the chain (the tanh limiter already exists).
- EarTrumpet: live peak indicators (VU meters already exist).
- Volume²: an OSD on volume change via a hotkey (a tray balloon is sufficient).
