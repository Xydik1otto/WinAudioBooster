; ============================================================
; WinAudioBooster — Inno Setup script
; Шлях у CI: packaging/innosetup/WinAudioBooster-Setup.iss
; Запуск локально: ISCC packaging/innosetup/WinAudioBooster-Setup.iss
; Шляхи Source задані відносно кореня репозиторію (ISCC запускається з кореня),
; OutputDir — теж відносний до кореня.
; ============================================================

#define MyAppName "WinAudioBooster"
#define MyAppVersion "1.1.0"
#define MyAppPublisher "Xydik1otto"
#define MyAppURL "https://github.com/Xydik1otto/WinAudioBooster"
#define MyAppExeName "WinAudioBooster.exe"

[Setup]
AppId={{7E1F2C4A-9B6D-4E0F-8A3C-1D2E5F6A7B8C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
; Uninstall.ps1 виконується перед видаленням — повертає системний вивід
; з VB-Cable на попередній фізичний пристрій, якщо бустер ще активний.
UninstallFilesDir={app}\uninstall
OutputDir=build\installer
OutputBaseFilename=WinAudioBooster-Setup-{#MyAppVersion}
; SetupIconFile=WinAudioBooster\Assets\app.ico  ; увімкніть, коли з'явиться іконка додатка (зараз відсутня в репо)
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog commandline

[Languages]
Name: "ukrainian"; MessagesFile: "compiler:Languages\Ukrainian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
; Single-file exe з dotnet publish (CI) або локальної збірки
Source: "publish\WinAudioBooster.exe"; DestDir: "{app}"; Flags: ignoreversion
; Додаткові нативні DLL, які dotnet publish кладе поруч з exe
Source: "publish\*.dll"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

; Примітка: щоб [UninstallRun] знайшов Uninstall.ps1, додайте його до [Files]:
;   Source: "Deployment\Uninstall.ps1"; DestDir: "{app}\uninstall"; Flags: ignoreversion
; Секцію [UninstallRun] можна залишити — за відсутності файлу Inno лише пропустить крок
; ( powershell зупиниться з помилкою, але видаляти файли це не заважає).
