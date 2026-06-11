param(
    [string]$Configuration = 'Release',
    [string]$OutputDir = (Join-Path $PSScriptRoot '..\publish')
)

$ErrorActionPreference = 'Stop'
$ProjectDir = Resolve-Path (Join-Path $PSScriptRoot '..')
$Csproj = Join-Path $ProjectDir 'AnyTray.csproj'

function Test-DotNet8 {
    try {
        $ver = (& dotnet --version 2>$null)
        if ($ver -and $ver.StartsWith('8.')) { return $true }
    } catch {}
    return $false
}

Write-Host "=== AnyTray Build ===" -ForegroundColor Cyan

# 1. Проверяем .NET 8 SDK
if (-not (Test-DotNet8)) {
    Write-Host ".NET 8 SDK не найден. Устанавливаю через winget..." -ForegroundColor Yellow

    $winget = (Get-Command winget -ErrorAction SilentlyContinue)?.Source
    if (-not $winget) {
        Write-Error "winget не найден. Установи .NET 8 SDK вручную: https://dotnet.microsoft.com/download/dotnet/8.0"
        exit 1
    }

    # winget ID для .NET 8 SDK
    & $winget install --id Microsoft.DotNet.SDK.8 --silent --accept-package-agreements --accept-source-agreements
    if ($LASTEXITCODE -ne 0 -and $LASTEXITCODE -ne -1978335189) {  # -1978335189 = уже установлен
        Write-Error "Не удалось установить .NET 8 SDK через winget. Код: $LASTEXITCODE"
        exit 1
    }

    # Перезагружаем PATH чтобы dotnet стал доступен
    $env:Path = [System.Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' +
                [System.Environment]::GetEnvironmentVariable('Path', 'User')

    if (-not (Test-DotNet8)) {
        Write-Error ".NET 8 SDK установлен, но dotnet не найден в PATH. Перезапусти терминал и повтори."
        exit 1
    }
    Write-Host ".NET 8 SDK установлен." -ForegroundColor Green
} else {
    Write-Host ".NET 8 SDK уже установлен: $(dotnet --version)" -ForegroundColor Green
}

# 2. Восстановление пакетов
Write-Host "`nВосстановление NuGet-пакетов..." -ForegroundColor Cyan
& dotnet restore $Csproj
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# 3. Сборка
Write-Host "`nСборка $Configuration..." -ForegroundColor Cyan
& dotnet build $Csproj -c $Configuration
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# 4. Публикация (опционально, если указан OutputDir)
if ($OutputDir) {
    Write-Host "`nПубликация в $OutputDir..." -ForegroundColor Cyan
    if (-not (Test-Path $OutputDir)) { New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null }
    & dotnet publish $Csproj -c $Configuration -r win-x64 --self-contained false -o $OutputDir
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    $exe = Join-Path $OutputDir 'AnyTray.exe'
    if (Test-Path $exe) {
        Write-Host "`n✅ Готово!" -ForegroundColor Green
        Write-Host "   EXE: $exe" -ForegroundColor White
        Write-Host "   Версия: $((Get-Item $exe).VersionInfo.FileVersion)" -ForegroundColor White
    }
}

Write-Host "`nНажми любую клавишу..."
[void][System.Console]::ReadKey($true)
