# WinAudioBooster APO Integration: Architecture Plan

> Status: **planned direction / starter package**. Nothing in the existing code, CI, or
> distribution of WinAudioBooster has changed — everything below is additive preparation.
> The APO skeleton lives in `native\WinAudioApo\`; the distribution plan is in
> `packaging\apo\README.md`.

## 1. What an APO is in the Windows audio stack

An **APO (Audio Processing Object)** is a user-mode COM component that the Windows
audio engine (*audiodg.exe*) inserts directly into the signal-processing graph of a
given audio endpoint. It was introduced into the stack with Windows Vista as part of
the "system effects infrastructure".

APO types by position in the graph:

| Type | Position | Relevance to us |
|-----|-----------|-----------------|
| **SFX** (stream effect) | before the mixer, one per stream | potential (per-app processing) |
| **MFX** (mode effect) | after the mode mixer (raw/default) | possible second stage |
| **LFX** (local effect) / **GFX** (global effect) | the classic Vista pair: LFX before the mixer, GFX after | the main path (as in Equalizer APO) |

Required COM interfaces (see [Implementing Audio Processing Objects][msdn-apo]):
`IAudioProcessingObject` (initialization, format negotiation),
`IAudioProcessingObjectConfiguration` (`LockForProcess`/`UnlockForProcess`),
`IAudioProcessingObjectRT` (`APOProcess` — the actual DSP on the audio engine's RT
thread), plus `IAudioSystemEffects(2|3)`, without which the audio engine does not
recognize the DLL as a system effects APO. Windows 11 (build 22000+) has a newer API
set ([Windows 11 APIs for APOs][msdn-apo-win11]) — we deliberately do **not** use it
initially, because it requires shipping the APO together with a driver.

## 2. Why this removes VB-Cable and latency

WinAudioBooster currently works like this:

```
app → [VB-Cable virtual device] → (kernel) → our WPF process:
      NAudio WASAPI loopback → DspProcessor (gain→EQ→shelves→widen→
      crossfeed→balance/mono→compressor→AGC→limiter) → WPF device → speakers
```

That is two full stream handoffs through the kernel plus WASAPI loops inside our
process. Each hop adds latency (buffer period size, scheduling jitter), and VB-Cable
also has a fixed buffer in its own driver. Overall we pay tens of milliseconds plus
extra CPU for copying samples back and forth.

With an APO:

```
app → audiodg.exe: mixer → [WinAudioApo: the same DSP chain in C++] → driver → device
```

- **No additional virtual devices**: the APO lives inside the standard audio engine
  graph of a specific endpoint.
- **Minimal latency**: `GetLatency()` can honestly return 0 — processing is streamed,
  with no re-buffering (the same level at which Equalizer APO operates).
- **System-wide**: all applications are processed at once — the very reason VB-Cable
  is currently deployed.
- **VB-Cable is removed from the chain entirely** (uninstalled); the WPF application
  remains only a configurator and stops being critical for audio: if the WPF process
  is not running, audio goes through the last applied configuration or bypasses it.

## 3. How APO injection works (registry model)

Note a factual correction: there is no universal "RegApo" tool in Equalizer APO —
its Configurator performs the registry writes itself (see `DeviceAPOInfo.cpp`/
`Configurator` in [their source][eqapo-src]); the WDK provides `RegisterAPO`/
`UnregisterAPO` functions for the same purpose, declared in `audioenginebaseapo.h`.
The phrase "RegApo-style registration" below refers to this approach. Our
`DllRegisterServer` performs part of the writes itself (skeleton:
`native\WinAudioApo\WinApoReg.cpp`).

For the audio engine to load our APO, the following is required (recipe:
[Equalizer APO developer documentation][eqapo-devdoc] + [MSDN][msdn-apo]):

1. **COM class registration** — `HKLM\SOFTWARE\Classes\CLSID\{GUID}`
   (visible as `HKCR\CLSID\...`): default value = class name, `InprocServer32`
   subkey = path to the DLL + `ThreadingModel` = `Both`.
2. **Registration as an Audio Processing Object** — the key
   `HKLM\SOFTWARE\Classes\AudioEngine\AudioProcessingObjects\{GUID}`
   (friendly name, copyright, CreatedOn, version, flags). This is "normally done by
   the RegisterAPO function from the DDK" — marked as a TODO in our skeleton.
3. **Attachment to a device** — in
   `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render\{endpoint GUID}\FxProperties`
   the `{…}\{…},0` REG_SZ values contain the CLSID of the effect (the classic
   `Lfx`/`Gfx` values plus "APO effect fallthrough" pairs). **Existing values must be
   backed up** (Equalizer APO keeps them under `HKLM\SOFTWARE\EqualizerAPO\Child APOs`)
   and our APO attached as a chain on top — otherwise the driver's stock effects are
   lost.
4. **Restart of the audio service** (`net stop audiosrv / net start audiosrv`, or
   restarting `audiodg` — in practice a reboot is simpler) — the audio engine
   re-reads the graph only then.

## 4. Limitations (read before attempting to install)

- **Shared mode only.** An APO works in the shared mode of the audio engine. In
  exclusive mode (ASIO-like, WASAPI exclusive) our effects do not apply at all —
  same as Equalizer APO. For a booster this is an acceptable compromise.
- **Signing.** An APO is loaded into the protected process *audiodg.exe*, so Windows
  checks the signature. For development/testing set
  `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Audio` → DWORD
  `DisableProtectedAudioDG = 1` (a self-signed/unsigned APO then loads). Risk:
  applications with a secure audio path (some DRM players, protected video content)
  may refuse to output audio. For public distribution a signed DLL is required
  (EV/OV certificate plus the HLK approach for a full driver-level delivery — see
  [Windows 11 APO APIs][msdn-apo-win11]).
- **Architectures.** The APO is loaded into the *audiodg.exe* of the same bitness as
  the OS: on x64 Windows — **only an x64** DLL (x86 would only be needed for a 32-bit
  Windows 10; x86 does not exist on Windows 11), on Windows on ARM — ARM64.
  `WinAudioApo.vcxproj` currently contains Win32|x64; ARM64 is a TODO (add the
  platform to the vcxproj, build on an ARM64 agent).
- **A config update is not an APO reload**: the APO reads its config from a file (our
  approach), so the WPF configurator writes the file and the sound changes without
  re-registration. But a change of **registry** settings (attachment to another
  device) requires an audio service restart.
- **Testing is harder than for a regular exe**: an APO crash is a crash of audio for
  all applications. Keep the first versions "simple and dumb"; test in a VM or on a
  second machine.

## 5. WinAudioBooster migration plan

Target architecture:

```
┌────────────────────────┐        config (INI)         ┌──────────────────────────┐
│ WinAudioBooster (WPF)  │ ── %APPDATA%\WinAudioBooster │ audiodg.exe              │
│  - configurator UI     │    \apo-config.ini           │  mixer → WinAudioApo:    │
│  - presets, EQ curves  │ ◄── (TODO: Named pipe for    │  gain→10-band EQ→        │
│  - APO diagnostics     │     instant push, as in      │  limiter (RT thread)     │
│  - APO installer       │     Equalizer APO)           │                          │
└────────────────────────┘                              └──────────────────────────┘
```

Division of responsibilities: **WinAudioApo (C++)** runs the DSP on the audio
engine's RT thread; **the WPF app** is the configurator (the same UI: gain/EQ/limiter
sliders, presets) and no longer passes audio through itself.

### Phase 0 — skeleton (done in this package)

- `native\WinAudioApo\`: a DLL with the APO class, factory, CLSID registration, a
  gain→10-band biquad EQ→tanh-limiter chain, and a parser for
  `%APPDATA%\WinAudioBooster\apo-config.ini` with a timestamp check on every chunk.
  Parameter names conceptually match `Models/AppSettings`:
  `GainDb`, `EqGainsDb[10]` (bands 31/62/125/250/500/1k/2k/4k/8k/16k, Q=1.2,
  ±12 dB), `LimiterCeilingDb`.
- Documentation (this file plus the READMEs in native and packaging).

### Phase 1 — make it produce sound (requires VS 2022 + WDK)

- Fill in all `TODO:` items in the native code: calling `RegisterAPO` from the DDK,
  format checks, silent-frame handling, ARM64.
- Build, register (`regsvr32` plus attachment via FxProperties or a RegApo-style
  script from `packaging\apo`), verify on a dev machine with
  `DisableProtectedAudioDG=1`.
- Audio parity: verify behavior against the C# `DspProcessor` (slew 60 dB/s,
  knee limiter at 0.9 of the ceiling, the RBJ peaking EQ formulas match NAudio).

### Phase 2 — configurator synchronization

- The WPF app writes `apo-config.ini` on every change (atomic write+rename).
- The APO re-reads it by timestamp (already in the skeleton). Next step — a named
  pipe or a message window for instant push without a stat on every chunk.
- Shared preset export/import (roughly `presets.json` → INI sections).

### Phase 3 — distribution and retiring "disable VB-Cable"

- Installer from `packaging\apo` (DLL copy + registration under admin +
  backup/rollback, see `packaging\apo\README.md`).
- UI: an "enable system mode" wizard (checks the architecture, sets
  `DisableProtectedAudioDG` for dev builds, attaches the APO, restarts audio).
- Once stable — remove the VB-Cable path from the default flow, keep it as a legacy
  mode.

## 6. References

- [Implementing Audio Processing Objects — Microsoft Learn][msdn-apo]
- [Audio Processing Object Architecture — Microsoft Learn][msdn-arch]
- [Windows 11 APIs for Audio Processing Objects][msdn-apo-win11]
- SYSVAD APO samples: <https://github.com/microsoft/Windows-driver-samples/tree/main/audio/sysvad/APO>
  (MIT; we adapted the *pattern*, not the source text)
- [Equalizer APO — Developer documentation (SourceForge wiki)][eqapo-devdoc] —
  the most detailed description of injecting a third-party APO through the registry
- [Equalizer APO source (mirror, GPL-2.0)][eqapo-src]
- Microsoft Audio (APO samples from the Windows Audio team):
  <https://github.com/microsoft/audio>

[msdn-apo]: https://learn.microsoft.com/en-us/windows-hardware/drivers/audio/implementing-audio-processing-objects
[msdn-arch]: https://learn.microsoft.com/en-us/windows-hardware/drivers/audio/audio-processing-object-architecture
[msdn-apo-win11]: https://learn.microsoft.com/en-us/windows-hardware/drivers/audio/windows-11-apis-for-audio-processing-objects
[eqapo-devdoc]: https://sourceforge.net/p/equalizerapo/wiki/Developer%20documentation/
[eqapo-src]: https://github.com/mirror/equalizerapo
