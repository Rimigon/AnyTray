# Очистка кэша иконок Windows и перезапуск проводника
# Запускать от имени текущего пользователя (не обязательно админ)

$ErrorActionPreference = 'Stop'

Write-Host "Останавливаю проводник..." -ForegroundColor Yellow
Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500

Write-Host "Чищу кэш иконок..." -ForegroundColor Yellow
$cachePaths = @(
    "$env:LOCALAPPDATA\Microsoft\Windows\Explorer\iconcache*.db"
    "$env:LOCALAPPDATA\Microsoft\Windows\Explorer\thumbcache*.db"
)
foreach ($p in $cachePaths) {
    Remove-Item $p -Force -ErrorAction SilentlyContinue
}

Write-Host "Перезапускаю проводник..." -ForegroundColor Green
Start-Process explorer

Write-Host "`nГотово. Теперь иконка должна обновиться в проводнике и на ярлыках." -ForegroundColor Cyan
Write-Host "Если иконка на панели задач / рабочем столе НЕ обновилась — пересоздай ярлык вручную." -ForegroundColor Yellow
