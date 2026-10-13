$ErrorActionPreference = 'Stop'
$planerRoot = Split-Path -Parent $PSScriptRoot
Push-Location $planerRoot
try {
    & "$PSScriptRoot\dotnet.ps1" publish src/Planer.App/Planer.App.csproj --configuration Release --runtime win-x64 --self-contained true --output artifacts/publish/win-x64 /p:Platform=x64 /p:PublishTrimmed=false /p:PublishReadyToRun=false /p:WindowsAppSDKSelfContained=true /p:TargetLatestRuntimePatch=true
    if ($LASTEXITCODE -ne 0) { throw 'Публикация приложения завершилась ошибкой' }
    & "$PSScriptRoot\verify-ui.ps1" -Executable (Join-Path $planerRoot 'artifacts\publish\win-x64\Planer.App.exe')
    $planerIsccCandidates = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
        (Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Links\ISCC.exe')
    )
    $planerCompiler = $planerIsccCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (!$planerCompiler) { $planerCompiler = (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source }
    if (!$planerCompiler) { throw 'Установите Inno Setup 6: winget install --id JRSoftware.InnoSetup --exact' }
    & $planerCompiler /Qp installer/Planer.iss
    if ($LASTEXITCODE -ne 0) { throw 'Компиляция установщика завершилась ошибкой' }
    Get-Item -LiteralPath (Join-Path $planerRoot 'dist\Planer-1.0.0-win-x64-setup.exe') | Select-Object FullName, Length
}
finally { Pop-Location }
