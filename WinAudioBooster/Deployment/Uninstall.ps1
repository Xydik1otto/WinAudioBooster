# WinAudioBooster PowerShell Cleanup Script
param(
    [switch]$RemoveUserData = $false
)

Write-Host "Зупинка процесів WinAudioBooster..." -ForegroundColor Yellow
Get-Process -Name "WinAudioBooster" -ErrorAction SilentlyContinue | Stop-Process -Force

Write-Host "Видалення запису з автозапуску Windows..." -ForegroundColor Yellow
$regPath = "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"
if (Get-ItemProperty -Path $regPath -Name "WinAudioBooster" -ErrorAction SilentlyContinue) {
    Remove-ItemProperty -Path $regPath -Name "WinAudioBooster" -ErrorAction SilentlyContinue
}

if ($RemoveUserData) {
    Write-Host "Видалення конфігурацій та пресетів..." -ForegroundColor Red
    $appDir = $PSScriptRoot
    Remove-Item -Path "$appDir\settings.json" -ErrorAction SilentlyContinue
    Remove-Item -Path "$appDir\presets.json" -ErrorAction SilentlyContinue
}

Write-Host "Очищення завершено успішно!" -ForegroundColor Green