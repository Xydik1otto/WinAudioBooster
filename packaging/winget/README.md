# Submitting the winget Manifest to microsoft/winget-pkgs

This guide describes how to publish WinAudioBooster in the
[Windows Package Manager](https://github.com/microsoft/winget-pkgs) repository.

## Step 1. Get the real SHA256

After the `v1.1.0` release ships:

```bash
curl -LO https://github.com/Xydik1otto/WinAudioBooster/releases/download/v1.1.0/WinAudioBooster.exe
certutil -hashfile WinAudioBooster.exe SHA256   # or sha256sum WinAudioBooster.exe
```

Replace the `0000...` placeholder in `Xydik1otto.WinAudioBooster.yaml` (the
`InstallerSha256` field) and in `packaging/scoop/winaudiobooster.json` (the `hash`
field).

## Step 2. Auto-generation (recommended)

The simplest path is [wingetcreate](https://github.com/microsoft/winget-create):

```bash
winget install Microsoft.WingetCreate
wingetcreate new https://github.com/Xydik1otto/WinAudioBooster/releases/download/v1.1.0/WinAudioBooster.exe
# the tool fills in the manifest (including SHA256) and saves the manifest parts locally
wingetcreate submit --token <GITHUB_TOKEN> <path_to_folder_with_manifest>
```

`wingetcreate` generates a **multi-file** manifest right away — exactly the format
the repository expects.

## Step 3. Manual submission

1. Split the single-file manifest into parts (versions 1.0.0 / 1.1 / 1.2 / 1.4):
   - `Xydik1otto.WinAudioBooster.yaml` (Launcher / Version),
   - `Xydik1otto.WinAudioBooster.installer.yaml` (InstallerType portable, InstallerUrl, InstallerSha256),
   - `Xydik1otto.WinAudioBooster.locale.uk-UA.yaml` (ShortDescription, Tags, Publisher...),
   - `Xydik1otto.WinAudioBooster.locale.en-US.yaml`,
2. Open a PR in [microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs) under the
   `manifests/x/Xydik1otto/WinAudioBooster/1.1.0/` folder.
3. Name the PR `PackageIdentifier Xydik1otto.WinAudioBooster version 1.1.0`.
4. Wait for bot validation (manifest validator) and review.

## Step 4. Local verification

```bash
winget install --manifest packaging/winget/Xydik1otto.WinAudioBooster.yaml
# or for the multi-file form:
winget validate <manifest_folder>
```

## Notes

- The `ManifestVersion: 1.4.0` schema was current at the time of writing; check the
  repository requirements.
- `InstallerType: portable` corresponds to a single-file exe without an installer.
- Version updates follow the same PR process (a new folder named after the version).
