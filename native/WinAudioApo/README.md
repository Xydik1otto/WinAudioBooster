# WinAudioApo — Starter Skeleton of a System APO for WinAudioBooster

> **Status: starter skeleton.** Structurally correct, but contains `TODO` marks in
> places that depend on a specific WDK/SDK version or require dev verification on a
> real machine. Context and the overall plan are in
> [`docs\apo\README.md`](../../docs/apo/README.md).

## What this is

A user-mode COM DLL that the Windows audio engine (*audiodg.exe*) loads as a system
effects APO for a given endpoint. The DSP chain: **gain (slew 60 dB/s) →
10-band peaking EQ (biquad, RBJ) → tanh limiter** — parity with
`WinAudioBooster/Audio/DspProcessor.cs`. Parameters are read from
`%APPDATA%\WinAudioBooster\apo-config.ini` (`GainDb`, `EqGainsDb` (10 values,
comma-separated), `LimiterCeilingDb`); re-reading happens by LastWriteTime on every
chunk (a FileSystemWatcher-style approach without an extra thread).

Key files:

| File | Purpose |
|------|-------------|
| `WinApo.h/.cpp` | The `CWinApo` class (IAudioProcessingObject / RT / SystemEffects2/3) + DSP |
| `ApoFact.h/.cpp` | Hand-written `IClassFactory`, `DllGetClassObject`, `DllCanUnloadNow`, `DllRegisterServer/UnregisterServer`, `DllMain` |
| `WinApoReg.cpp` | COM class registry entries (CLSID/InprocServer32) + TODO on `RegisterAPO` |
| `ApoConfig.h/.cpp` | INI parser + timestamp check |
| `WinAudioApo.def` | DLL exports |
| `WinAudioApo.vcxproj` | VS 2022 (v143), Win32/x64, WindowsTargetPlatformVersion 10.0 |
| `apo-config.ini.example` | Sample config |
| `LICENSE-NOTE.md` | License note (BSD-2-Clause; Equalizer APO is GPL-2.0, no code was copied) |

GUIDs (baked in, do not regenerate):
- APO CLSID: `{FA1C7572-2A88-41EE-B5D0-D4B8B0B04557}`
- Effect GUID (for `GetEffectsList`): `{52461AC9-574E-46E7-A0E8-77AB1E43827A}`

## Build prerequisites

1. **Visual Studio 2022** (the "Desktop development with C++" workload, toolset v143).
2. **Windows 10/11 SDK** (any recent one; `WindowsTargetPlatformVersion=10.0`
   means "the latest installed").
3. **Windows Driver Kit (WDK)** — the APO helpers come from it
   (`BaseAudioProcessingObject.h`, the `audiomediatypecrt.lib` and
   `audioeng.lib` libraries, the `RegisterAPO` function). The DLL itself is pure
   user mode, but without the WDK paths the linker will not find the APO libraries:
   add the `$(WDK_ContentRoot)` lib paths to the project properties (or via a
   `.props` file), as the sysvad samples do. In SDK >= 10.0.22000 the
   `audioenginebaseapo.h` / `BaseAudioProcessingObject.h` headers are also present
   in the SDK itself.

Command-line build (Developer Command Prompt):

```bat
msbuild native\WinAudioApo\WinAudioApo.vcxproj /p:Configuration=Release /p:Platform=x64
```

Configurations: Debug/Release x Win32/x64. On 64-bit Windows **only the x64** DLL
actually loads into audiodg (Win32 is for 32-bit Windows 10; ARM64 is a TODO, see
the vcxproj).

## Registration / deregistration

The skeleton registers the **COM class** by itself:

```bat
regsvr32 /s WinAudioApo.dll      :: register (as administrator)
regsvr32 /u WinAudioApo.dll      :: deregister
```

This writes `HKLM\SOFTWARE\Classes\CLSID\{FA1C7572-...}` (InprocServer32,
ThreadingModel=Both) and the base key
`HKLM\SOFTWARE\Classes\AudioEngine\AudioProcessingObjects\{FA1C7572-...}`.

**But this is not enough** — before the first run, close the TODOs in
`WinApoReg.cpp`:

- the full set of `AudioProcessingObjects\{GUID}` values is produced by the
  `RegisterAPO`/`UnregisterAPO` functions from `audioenginebaseapo.h` (DDK) — add a
  call with `APO_REG_PROPERTIES` for `CWinApo`; or
- use a RegApo-style external script/tool from `packaging\apo` (the way Equalizer
  APO's Configurator does it, see [their dev docs](https://sourceforge.net/p/equalizerapo/wiki/Developer%20documentation/)).

## Attaching to a device (FxProperties)

1. Dev-machine prerequisite: `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Audio`
   → DWORD `DisableProtectedAudioDG = 1` (allow an unsigned APO).
2. In `HKLM\...\MMDevices\Audio\Render\{endpoint GUID}\FxProperties`, replace the
   effect value with the CLSID of our APO, **saving the previous values as a backup**
   (Equalizer APO keeps copies under `HKLM\SOFTWARE\EqualizerAPO\Child APOs` —
   we will do the same, see `packaging\apo\README.md`).
3. Restart audio: `net stop audiosrv && net start audiosrv` (or reboot).
4. Rollback = restore the backup values + restart the audio service.

## Testing

- **AudioEng.exe** from the WDK (`<WDK>\Tools\<arch>\audiodg` companion tools):
  `AudioEng.exe -a -apo {FA1C7572-2A88-41EE-B5D0-D4B8B0B04557}` — the official
  check of the APO interfaces (format calls, Lock/Unlock, APOProcess).
  An alternative from newer WDKs: `apointer` / test harnesses from the WDK
  "Audio APO tests".
- Manual smoke test: register, attach to a device, play audio, edit
  `apo-config.ini` — a gain/EQ change affects the sound without a restart.
- Watch `OutputDebugString` in a debugger attached to audiodg.exe (or DebugView) —
  TODO: add logging of the first load/config version.

## Known skeleton limitations

- Only `gain → 10-band EQ → limiter` is implemented; the rest of the DspProcessor
  chain (shelves, widen, crossfeed, mono/balance, compressor, AGC) is a TODO (see
  the comments in `WinApo.h`).
- Config re-reading happens on the RT thread (one stat per chunk — cheap; the parse
  itself only on change). TODO: parse on a background thread + atomic publication
  (Equalizer APO does exactly that).
- The format negotiation is simplified (float32 is accepted as is); format checks
  are a TODO.
- Silent-frame handling (`APO_CONNECTION_PROPERTY_FLAG_SILENCE`) is missing — TODO.
- APO value registration (`AudioProcessingObjects`) is incomplete without
  `RegisterAPO` — TODO.
- No signed binary: public distribution requires signing (see
  `packaging\apo\README.md`).
