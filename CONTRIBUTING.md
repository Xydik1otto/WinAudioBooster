# Contributing

Thank you for your interest in **WinAudioBooster**. This document describes how to build the project, which code style rules apply in the repository, and how to submit a pull request.

## Building the project

The **.NET 8 SDK** is required (the version is pinned in `global.json`).

```bash
# clone
git clone https://github.com/Xydik1otto/WinAudioBooster.git
cd WinAudioBooster

# restore dependencies and build
dotnet restore WinAudioBooster/WinAudioBooster.csproj
dotnet build WinAudioBooster/WinAudioBooster.csproj --configuration Release

# run tests
dotnet test WinAudioBooster.Tests/WinAudioBooster.Tests.csproj --configuration Release
```

Single-file publish (the same command CI uses for releases):

```bash
dotnet publish WinAudioBooster/WinAudioBooster.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The resulting exe: `WinAudioBooster/bin/Release/net8.0-windows/win-x64/publish/WinAudioBooster.exe`.

For local runs you will need **VB-Audio CABLE** — the application offers to install it on first start.

## Code style

- The interface language and **code comments are in Ukrainian** (identifiers are in English).
- Follow standard C# conventions (.editorconfig / Visual Studio default formatting).
- Change DSP logic, WASAPI handling, and device switching carefully and describe the changes in the PR — this is the most sensitive part of the code.
- Do not add new dependencies without a good reason; if you add one, explain why.
- Change the `WinAudioBooster.Tests\` test project and the main project only within the scope of your task.

## Pull request process

1. Create a branch from `main`: `feat/<name>` or `fix/<name>`.
2. If you change behavior, add or update tests in `WinAudioBooster.Tests`.
3. Make sure `dotnet build` and `dotnet test` pass locally (CI checks this on every PR).
4. Fill in the PR template (`.github/PULL_REQUEST_TEMPLATE.md`): what changed, why, and how it was verified.
5. Do not touch release files (`CHANGELOG.md` is updated at release time) — this is done by the maintainer.
6. Wait for review; small fixes should be separate commits, and the history does not need to be squashed before merge.

## Good first issues

Looking for a place to start? Typical first-PR tasks:

- **Themes** — add a new UI theme (see the existing Win11 Fluent / Arch / XP / StudioRack themes as a reference).
- **Localization** — add a new interface language or improve the existing UA/EN translations.
- **Presets** — add a built-in EQ/compressor preset or improve saving/import of custom presets.
- **Documentation** — refine the README, troubleshooting, translations.
- **Small bugs** — labeled `good first issue` in Issues.

Before starting work, leave a comment on the corresponding Issue to avoid duplicating effort.

## Questions

Usage questions go to Issues with the `question` label; for vulnerabilities see [SECURITY.md](SECURITY.md) (do not publish them in public Issues).
