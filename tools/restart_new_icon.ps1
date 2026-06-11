# Перезапуск AnyTray с новой иконкой
# 1. Убивает старый процесс
# 2. Очищает кэш иконок Windows
# 3. Запускает свежий exe из publish

$ErrorActionPreference = 'Stop'
$exe = Join-Path $PSScriptRoot '..\publish\AnyTray.exe'

if (-not (Test-Path $exe)) {
    Write-Error "Не найден: $exe`nСначала собери: dotnet publish -c Release -r win-x64 --self-contained false -o ./publish"
    exit 1
}

Write-Host "1. Останавливаю старый AnyTray..." -ForegroundColor Yellow
Get-Process AnyTray -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 300

Write-Host "2. Чищу кэш иконок..." -ForegroundColor Yellow
Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500
Remove-Item "$env:LOCALAPPDATA\Microsoft\Windows\Explorer\iconcache*.db" -Force -ErrorAction SilentlyContinue
Remove-Item "$env:LOCALAPPDATA\Microsoft\Windows\Explorer\thumbcache*.db" -Force -ErrorAction SilentlyContinue
Start-Process explorer
Start-Sleep -Milliseconds 500

Write-Host "3. Запускаю новый AnyTray..." -ForegroundColor Green
Start-Process $exe

Write-Host "`n✅ Готово! Смотри в трей — должна быть новая иконка 'окно с минусом'." -ForegroundColor Cyan
Write-Host "   Файл: $exe" -ForegroundColor DarkGray
