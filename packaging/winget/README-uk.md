# Сабміт winget-маніфесту в microsoft/winget-pkgs

Ця інструкція описує, як опублікувати WinAudioBooster у [Windows Package Manager](https://github.com/microsoft/winget-pkgs).

## Крок 1. Отримайте реальний SHA256

Після виходу релізу `v1.1.0`:

```bash
curl -LO https://github.com/Xydik1otto/WinAudioBooster/releases/download/v1.1.0/WinAudioBooster.exe
certutil -hashfile WinAudioBooster.exe SHA256   # або sha256sum WinAudioBooster.exe
```

Замініть заглушку `0000...` у `Xydik1otto.WinAudioBooster.yaml` (поле `InstallerSha256`) та
в `packaging/scoop/winaudiobooster.json` (поле `hash`).

## Крок 2. Автогенерація (рекомендовано)

Найпростіший шлях — [wingetcreate](https://github.com/microsoft/winget-create):

```bash
winget install Microsoft.WingetCreate
wingetcreate new https://github.com/Xydik1otto/WinAudioBooster/releases/download/v1.1.0/WinAudioBooster.exe
# інструмент заповнить маніфест (включно з SHA256) і збереже частини маніфесту локально
wingetcreate submit --token <GITHUB_TOKEN> <шлях_до_папки_з_маніфестом>
```

`wingetcreate` одразу генерує **мульти-файловий** маніфест — саме такий формат очікує репозиторій.

## Крок 3. Ручний сабміт

1. Розбийте single-file маніфест на частини (версії 1.0.0 / 1.1 / 1.2 / 1.4):
   - `Xydik1otto.WinAudioBooster.yaml` (Launcher / Version),
   - `Xydik1otto.WinAudioBooster.installer.yaml` (InstallerType portable, InstallerUrl, InstallerSha256),
   - `Xydik1otto.WinAudioBooster.locale.uk-UA.yaml` (ShortDescription, Tags, Publisher...),
   - `Xydik1otto.WinAudioBooster.locale.en-US.yaml`,
2. Створіть PR у [microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs) у папку
   `manifests/x/Xydik1otto/WinAudioBooster/1.1.0/`.
3. У назві PR вкажіть `PackageIdentifier Xydik1otto.WinAudioBooster version 1.1.0`.
4. Дочекайтеся валідації ботом (manifest validator) і рев'ю.

## Крок 4. Перевірка локально

```bash
winget install --manifest packaging/winget/Xydik1otto.WinAudioBooster.yaml
# або для мульти-файлового:
winget validate <папка_маніфесту>
```

## Примітки

- Схема `ManifestVersion: 1.4.0` — актуальна на момент написання; перевіряйте вимоги репозиторію.
- `InstallerType: portable` відповідає single-file exe без інсталятора.
- Оновлення версій робляться тим самим PR-процесом (нова папка з номером версії).
