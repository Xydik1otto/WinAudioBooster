# LICENSE-NOTE.md — License Note for native\WinAudioApo

The code in `native\WinAudioApo\` is to be licensed under **BSD-2-Clause** (like the
rest of WinAudioBooster), unless this conflicts with the project's primary license
(check the root `LICENSE` and reconcile the notes before release).

## Where the pattern comes from (important for compatibility)

| Source | License | What was used |
|---------|----------|----------------|
| Microsoft SYSVAD APO samples (`microsoft/Windows-driver-samples`, `audio/sysvad/APO`) | **MIT** | The architectural pattern: `CBaseAudioProcessingObject`, the class factory, handling of `APO_CONNECTION_PROPERTY`, interface method signatures |
| Equalizer APO (SourceForge / GitHub mirrors) | **GPL-2.0** | Only the *documented approach* to registration (the set of registry keys, a config file as the communication channel with the UI) — **not a single line of source code was copied** |
| Official Microsoft documentation (Implementing APOs, APO Architecture) | MSDN TOU | Interfaces, limitations, signatures |

## Correction of a common misconception

Internal project notes sometimes describe Equalizer APO as "BSD-2-Clause" —
**this is incorrect**: the official license of Equalizer APO is **GNU GPL v2**
(see the `LICENSE` in their repository and the SourceForge page). GPL is
incompatible with BSD/MIT distribution in the sense that code derived from GPL must
remain GPL.

The practical conclusion for WinAudioBooster:

1. **We do not copy Equalizer APO source** (RegApo/Configurator/EqualizerAPO
   DLL etc.) into BSD-licensed code. Only the publicly documented registry scheme is
   used — the set of keys/values itself is a way of interacting with Windows, not an
   object of copyright.
2. The `WinAudioApo` skeleton was written from scratch following the pattern of the
   **MIT-licensed** Microsoft samples (sysvad) — their code could be copied with
   attribution safely, but here it was rewritten by hand for our chain as well.
3. If real code from Equalizer APO is ever needed (for example, a config-syntax
   parser), it imposes GPL on the whole file/module; in that case keep it in a
   separate GPL-licensed process/binary or rewrite it independently.

Therefore: the starter package can be safely released under BSD-2-Clause provided
rule (1) is observed.
