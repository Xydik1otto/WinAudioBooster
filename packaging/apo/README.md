# APO Distribution (packaging\apo) — Plan

> **Status: plan.** This package does not yet change any WinAudioBooster installer
> (winget/scoop/InnoSetup are untouched). This is a recipe for how a future build
> from `native\WinAudioApo\` should be distributed and rolled back.

## How Equalizer APO does it (reference)

Equalizer APO installs **under administrator rights** and does three things:

1. Copies `EqualizerAPO.dll` (+ 32/64-bit variants) into `Program Files`.
2. Registers the APO classes (regsvr32 equivalent + writes to the
   `CLSID\{GUID}` and `AudioEngine\AudioProcessingObjects\{GUID}` keys — in their
   code this is done by the Configurator; "RegApo"-style).
3. Attaches the APO to the devices the user selects: writes the CLSID into
   `HKLM\...\MMDevices\Audio\Render\{endpoint}\FxProperties`, **preserving the
   previous values** under `HKLM\SOFTWARE\EqualizerAPO\Child APOs`, and offers an
   audio restart/reboot.

Recipe source: [Developer documentation (SourceForge wiki)](https://sourceforge.net/p/equalizerapo/wiki/Developer%20documentation/).

## Our distribution plan (phase 3 from docs\apo\README.md)

### What is installed

```
<ProgramFiles>\WinAudioBooster\APO\
    WinAudioApo.x64.dll      <- Release|x64 (the main one for 64-bit Windows)
    WinAudioApo.x86.dll      <- for 32-bit Windows 10 (optional)
    WinAudioApo.ARM64.dll    <- TODO, once the platform appears in the vcxproj
    UninstallApo.exe / .cmd  <- rollback (below)
```

### Installer steps (administrator rights required)

1. Copy the DLLs into `Program Files\WinAudioBooster\APO\`.
2. `regsvr32 /s WinAudioApo.x64.dll` (COM class registration; our DLL can
   `DllRegisterServer` by itself) — plus writing the remaining
   `AudioEngine\AudioProcessingObjects\{GUID}` values (TODO `RegisterAPO` in
   `WinApoReg.cpp`, or a separate RegApo-style script).
3. For each selected endpoint:
   - read the current `FxProperties` values (the Lfx/Gfx effect pairs) and back
     them up under `HKLM\SOFTWARE\WinAudioBooster\APO Backup\{endpoint GUID}`
     (our analog of Child APOs);
   - write our CLSID (`{FA1C7572-2A88-41EE-B5D0-D4B8B0B04557}`);
   - if the APO is unsigned (dev/beta builds), set
     `HKLM\...\CurrentVersion\Audio → DisableProtectedAudioDG = 1` **only after
     explicit user consent** and a warning about DRM applications.
4. Offer an audio service restart / reboot.
5. Write to the uninstall log (HKLM Uninstall or InnoSetup `[UninstallRun]`).

### Rollback (critical)

- Uninstaller: `regsvr32 /u`, restore the backed-up `FxProperties` values from
  `HKLM\SOFTWARE\WinAudioBooster\APO Backup\`, remove
  `DisableProtectedAudioDG` (if it was set), delete the files.
- Every registry write must be **idempotent and reversible**; before writing,
  always read and store what was there first.
- Emergency mode: if audio "disappears" after installation, the user runs
  `UninstallApo.cmd`, which restores the backup and restarts audio. This must work
  even if our WPF application does not start at all.

### Risks

| Risk | Mitigation |
|-------|-----------|
| An APO crash = a crash of system audio | Ship only tested Release builds; error logging; one-command rollback |
| Secure audio path / DRM breaks from `DisableProtectedAudioDG` | Option off by default; document it; signed DLL for the release |
| Overwriting the driver's stock effects | Mandatory FxProperties backup + an "our APO on top of the child APO" chain (the Equalizer APO pattern) |
| A Windows update resets endpoint effects | Re-apply during WPF configurator startup (registry check + automatic restoration) |
| Architecture mismatch (ARM64, x86) | The installer picks the DLL for the OS architecture; on ARM64 refuse for now with an explanation |
| Signing | Release: Authenticode-signed DLL (like the main application); dev: DisableProtectedAudioDG |

## Relationship with the WPF configurator

- The configurator writes `%APPDATA%\WinAudioBooster\apo-config.ini`
  (`GainDb`, `EqGainsDb[10]`, `LimiterCeilingDb`) — the same format as
  `native\WinAudioApo\apo-config.ini.example`; the APO picks up changes by
  LastWriteTime without a restart.
- UI plan: a "System mode (APO)" section — installation/attachment status, an
  "Apply to device" button, a log of recent actions.
- TODO: an instant push channel (named pipe) instead of polling — see
  docs\apo\README.md, phase 2.
- Simplified user-facing install path: the WPF application invokes elevation (UAC)
  on a small helper that performs steps 1-4 above; all registry logic lives in the
  helper, not in the WPF app itself (so it can be re-invoked from the CLI for
  rollback).
