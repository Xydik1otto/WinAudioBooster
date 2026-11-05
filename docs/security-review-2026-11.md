# WinAudioBooster Security Review — 2026-11

> **Update 2026-11-05:** findings **#1, #2, #3 are fixed** after this review
> (commit `security fixes`): #1 → the SHA256 check of the update is now mandatory
> (fail-closed); #2 → the installer accepts only its own downloaded zip in `%TEMP%`
> (the search of parent directories was removed) plus a mandatory SHA256 check of
> the archive before the runas launch; #3 → PowerShell quoting `''` for the device
> id plus a fix of the syntactically broken block in the here-string.
> The LOW items (#4-#8) remain as documented risks.

The review was performed statically (code reading + git grep). Scope:
`WinAudioBooster\WinAudioBooster\` (net8.0-windows, WPF, self-contained win-x64).
The reviewer did not change code. All statements below are verified facts from the
files; where no problem was found, that is stated explicitly.

## Summary

| Severity | Count |
|---|---|
| CRITICAL | 0 |
| MEDIUM | 3 |
| LOW | 5 |

The main risks are in the update/driver supply chain: (1) auto-update is
**fail-open** — without a digest it runs an unverified exe; (2) the VB-Cable
installer executes the contents of a downloaded zip **with elevation (runas) and no
integrity check at all**; (3) injection into the PowerShell command line in the
device-switching fallback.

## Findings table

| # | Severity | File:lines | Problem |
|---|---|---|---|
| 1 | MEDIUM | `Services/UpdateService.cs:109,175–195,221` | **Fail-open SHA256 check of the auto-update.** If GitHub does not return a `digest` (older assets or a non-sha256 algorithm — `ExtractSha256FromDigest` returns an empty string, lines 260–278), `DownloadAndInstallUpdateAsync` only logs "check skipped" (line 194) and **still launches the downloaded exe** via a batch script (line 221). In other words, in the "no digest" branch the executable runs with no integrity check at all. The check itself is correct (lines 177–191), but by design it is optional (fail-open, not fail-closed). |
| 2 | MEDIUM | `Services/VbCableInstaller.cs:34–46,56,65–70,96–124` | **Running a third-party installer with administrator rights without an integrity check.** (a) The downloaded zip (lines 41–46) undergoes no verification — neither SHA256 nor an Authenticode-signature check of `VBCABLE_Setup*.exe`; it is then launched with `Verb = "runas"` (lines 65–70), i.e. elevated. A compromise of vb-audio.com/CDN or a MITM above TLS → admin RCE on the user's machine. (b) `FindValidZipArchiveByContent` (lines 96–124) searches for a local zip in `appDir` **and three parent directories** and uses it without verifying its origin; if the application is installed portably into a user-writable directory, any local process can plant a zip with a `VBCABLE_Setup*.exe` inside — it gets the runas launch. (c) The download URL is extracted by regex from the site's HTML (lines 126–137) — the host is restricted to `download.vb-audio.com`, but the path/file is uncontrolled. |
| 3 | MEDIUM | `Utils/AudioDeviceSwitcher.cs:52–81,93–109` | **Injection into the PowerShell command line.** `SetDeviceViaPowerShellHost` substitutes `'{deviceId}'` into a PowerShell script (line 75) that is passed to `powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "…"` (line 81). The value is **not escaped**: a single quote in a device name (real names like `John's Speakers`) breaks the string, and `ResolveToDeviceId` (lines 93–109) returns arbitrary input as-is (lines 95, 107), so a controlled value (a device name from settings/presets) can terminate the string and execute arbitrary PowerShell code as the user. Incidentally: `-ExecutionPolicy Bypass` + `Add-Type` is a pattern that triggers AV/EDR and widens the injection impact. |
| 4 | LOW | `Services/UpdateService.cs:125–135,207–221` | **TOCTOU / predictable paths in %TEMP%.** The downloaded exe (`%TEMP%\WinAudioBooster\update\WinAudioBooster.exe`) and `update.bat` sit at fixed, known-in-advance paths. Between the SHA256 check (lines 180–190) and the batch-script launch (221), the file can be swapped by another process of the same user; the batch script itself is also overwritten without exclusive creation. The user's %TEMP% is not accessible to other users, so the risk is limited to same-user code, but the check and launch are not atomic. |
| 5 | LOW | `MainWindow/MainWindow.Eq.cs:178` + `Utils/AutoEqImporter.cs:16–64` | **No file-size limit before reading.** `File.ReadAllText(dialog.FileName)` (line 178) reads the whole file without a size check; the entire content is then processed in memory (`Substring`, `Split`, a `List` of points). CSV injection is not a concern (the parser only reads numbers), but a gigabyte-sized file leads to memory exhaustion / a UI freeze (DoS). The file comes from a user-driven picker, hence LOW. |
| 6 | LOW | `Services/SettingsManager.cs:62` and `Utils/JsonHelper.cs:18` | **No length limit when reading JSON.** `File.ReadAllText` without a size limit before `JsonSerializer.Deserialize`. Deserialization itself is safe (System.Text.Json into typed models, `PropertyNameCaseInsensitive`, no polymorphic `$type` converters — verified); the risk is purely resource-related: a corrupted/tampered large local settings/preset file slows startup or consumes memory. |
| 7 | LOW | `Services/LoggingService.cs:16,58–61` | **Logging of potential PII by default.** Logging is enabled by default (`IsEnabled = true`, line 16); `WriteException` (lines 58–61) writes full stack traces to `%APPDATA%\WinAudioBooster\log.txt`, and some messages contain download URLs (UpdateService, line 136). The %APPDATA% path reveals the Windows user name; stack traces may contain exe/profile paths. The file sits in the user profile and rotation exists (2 MB) — hence LOW. |
| 8 | LOW | `VBCABLE_Driver_Pack45.zip` (in the repository root and in `WinAudioBooster/WinAudioBooster/`) | **A binary third-party archive committed to the repository twice.** This both bloats the repo and is a supply-chain hygiene issue: precisely such a local zip (per #2b) is picked up by the installer and launched with admin rights without verification. Recommendation: remove it from the repo, keep it outside git (LFS/download at build time with a pinned SHA256). |

## Verified clean (confirmed no issues)

- **Secrets:** `git grep -inE 'api[_-]?key|secret|password|token|credential'` across
  `*.cs/*.xaml/*.json/*.ini/*.csproj` — only false-positive matches
  (`CancellationToken`, `tokens` in the CSV parser). **No secrets found.**
- **TLS policies:** nowhere in the code is there a single
  `ServerCertificateCustomValidationCallback`, `ServicePointManager`,
  `DangerousAcceptAnyServerCertificate` — TLS validation is default, and all URLs
  are hardcoded HTTPS (`UpdateService.cs:12`, `VbCableInstaller.cs:10–11`).
- **Zip Slip:** `ZipFile.ExtractToDirectory(targetZipPath, extractPath)`
  (`VbCableInstaller.cs:56`) in .NET 8 has **built-in protection**: entries with
  `..`/absolute paths throw and do not escape the destination. No problem found
  (but do not rely on this alone in future changes — an explicit path check is
  still worthwhile).
- **Batch replacement exe — cmd injection:** `BuildUpdateBatchScript`
  (`UpdateService.cs:232–257`) substitutes only `Environment.ProcessPath`, a fixed
  temp path with a constant file name, and the temp directory — **no user input**;
  paths are quoted. No injection found. (Path traversal also does not apply: the
  path sources are not externally controlled.)
- **JSON deserialization:** System.Text.Json into typed classes without custom
  converters — safe by default; the only remark is the length limit (finding #6).
- **Win32 P/Invoke:** `HotkeyManager.cs:12–16` — `RegisterHotKey`/`UnregisterHotKey`
  with primitives (`uint`, `int`, `IntPtr`), **no buffers or marshaling of complex
  structures**; the `HwndHook` hook checks ids against registered ones. The COM
  declarations in `AudioDeviceSwitcher.cs:121–143` (PolicyConfig) are correct with
  `[PreserveSig]`/`LPWStr`. No problems found.
- **AutoStart:** `Services/AutoStartService.cs` — HKCU
  `...\CurrentVersion\Run`, the exe path is quoted (line 34). Clean.

## Recommendations by priority

1. **(Finding #2)** Add an integrity check to `VbCableInstaller`: a pinned SHA256 of
   the current `VBCABLE_Driver_Pack45.zip` in code (or an Authenticode-signature
   check of the VB-Audio publisher on `VBCABLE_Setup*.exe` before launch), fail-closed;
   narrow `FindValidZipArchiveByContent` to the application directory/a specific file.
2. **(Finding #1)** Make the SHA256 check in `UpdateService` fail-closed: if
   `expectedSha256` is empty, cancel the update (or use a fallback digest channel,
   e.g. a sidecar `SHA256SUMS` in the release) instead of running an unverified exe.
3. **(Finding #3)** Escape `deviceId` in `AudioDeviceSwitcher.SetDeviceViaPowerShellHost`
   (double `'` → `''`) or pass the argument safely (`-Command` with
   `-EncodedCommand`/base64, or a binary parameter via `ArgumentList`). Also fix the
   broken `opacity {{ … }}` branch (line 70) — the script is syntactically broken as
   it stands.
4. **(Finding #4)** Create the update temp directory with a random name
   (`Path.GetRandomFileName()`) and open the target file with
   `FileOptions.DeleteOnClose`/exclusive access.
5. **(Findings #5, #6)** Limit file sizes before reading (AutoEq — e.g. 5 MB;
   settings/presets — e.g. 1 MB) with an explicit error to the user.
6. **(Finding #7)** Disable logging by default or mask profile paths
   (`C:\Users\<name>` → `%USERPROFILE%`) and do not write full stack traces in
   release builds.
7. **(Finding #8)** Remove `VBCABLE_Driver_Pack45.zip` from the repository (both copies).
