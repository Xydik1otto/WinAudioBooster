# Security Policy

## Supported versions

Security fixes are released for the latest version from GitHub Releases.

| Version | Supported |
| ------- | --------- |
| 1.1.x   | Yes       |
| 1.0.x   | No (upgrade to the current version) |

## Reporting a vulnerability

**Please do not publish vulnerabilities in public Issues.**

1. **Recommended — GitHub Security Advisories:** use the *Security → Report a vulnerability* tab in the [Xydik1otto/WinAudioBooster](https://github.com/Xydik1otto/WinAudioBooster/security/advisories/new) repository. The dialog is private, and you can coordinate disclosure and credit with the maintainer.

2. **Alternative — email:** write to **`security@example.com`** *(placeholder — replace with the maintainer's real address)* with the subject `[security] WinAudioBooster: <short description>`.

## What to include in a report

- The application version (from GitHub Releases or `dotnet publish`);
- Reproduction steps / PoC;
- An impact assessment (what exactly an attacker could do);
- Your contact details for follow-up.

## What to expect

- Acknowledgement of receipt — within **72 hours**.
- Assessment and a fix plan — within 7 days.
- Fix release and coordinated disclosure by agreement (usually after the patch release ships).

## Scope

- Repository code (the WPF application, installation scripts, CI).
- Handling of local settings files (`appsettings.json`, `presets.json` in `%APPDATA%\WinAudioBooster`).
- Update checks via GitHub Releases (integrity of downloads).

Out-of-scope items (the VB-Audio CABLE driver, the VB-Audio driver pack itself) are not part of this project — report them directly to the vendor.
