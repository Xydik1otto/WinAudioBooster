; =======================================================
; WinAudioBooster Inno Setup Script
; =======================================================

#define MyAppName "WinAudioBooster"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Bigpi"
#define MyAppURL "https://github.com/bigpi/WinAudioBooster"
#define MyAppExeName "WinAudioBooster.exe"

[Setup]
; Унікальний ідентифікатор програми (AppId)
AppId={{C8A52E8F-4B12-43E1-9F22-82C4A214F2B3}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
; Встановлення без вимоги прав адміністратора (якщо потрібно для користувача)
PrivilegesRequired=lowest
OutputDir=.
OutputBaseFilename=WinAudioBooster-Setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "ukrainian"; MessagesFile: "compiler:Languages\Ukrainian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Шлях до збирання Release вашого проєкту .NET 8 (win-x64)
Source: "..\WinAudioBooster\bin\Release\net8.0-windows\win-x64\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\WinAudioBooster\bin\Release\net8.0-windows\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Включаємо PowerShell скрипт очищення в пакет встановлення
Source: "Uninstall.ps1"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; Опція запуску програми одразу після завершення інсталяції
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Автоматичний запуск PowerShell скрипта для очищення процесів та налаштувань при видаленні[cite: 20]
Filename: "powershell.exe"; Parameters: "-ExecutionPolicy Bypass -File ""{app}\Uninstall.ps1"" -RemoveUserData"; Flags: runhidden