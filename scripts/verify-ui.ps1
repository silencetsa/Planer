param([string]$Executable = '')
$ErrorActionPreference = 'Stop'
$planerRoot = Split-Path -Parent $PSScriptRoot
if (!$Executable) { $Executable = Join-Path $planerRoot 'src\Planer.App\bin\x64\Release\net10.0-windows10.0.26100.0\win-x64\Planer.App.exe' }
$Executable = (Resolve-Path -LiteralPath $Executable).Path
$planerDataDir = Join-Path $planerRoot 'artifacts\qa-data'
$planerImageDir = Join-Path $planerRoot 'artifacts\screenshots'
New-Item -ItemType Directory -Path $planerImageDir -Force | Out-Null
foreach ($planerScreen in @('Week','Month','Year','Habits','Payments','Today')) {
    $planerImagePath = Join-Path $planerImageDir ($planerScreen.ToLowerInvariant() + '.png')
    $planerErrorPath = $planerImagePath + '.error.txt'
    if (Test-Path -LiteralPath $planerErrorPath) { Remove-Item -LiteralPath $planerErrorPath }
    if (Test-Path -LiteralPath $planerImagePath) { Remove-Item -LiteralPath $planerImagePath }
    $planerArgs = @('--data-dir', ('"' + $planerDataDir + '"'), '--demo', '--view', $planerScreen, '--capture', ('"' + $planerImagePath + '"'))
    $planerProcess = Start-Process -FilePath $Executable -ArgumentList $planerArgs -WorkingDirectory (Split-Path -Parent $Executable) -WindowStyle Hidden -PassThru
    if (!$planerProcess.WaitForExit(15000)) {
        Stop-Process -Id $planerProcess.Id -Force -ErrorAction SilentlyContinue
        throw "Рендер $planerScreen не завершился за 15 секунд"
    }
    if (Test-Path -LiteralPath $planerErrorPath) { throw (Get-Content -LiteralPath $planerErrorPath -Raw) }
    if (!(Test-Path -LiteralPath $planerImagePath)) { throw "Рендер $planerScreen не создан; проверьте, не открыта ли QA-база другим процессом" }
    Get-Item -LiteralPath $planerImagePath | Select-Object Name, Length
}
