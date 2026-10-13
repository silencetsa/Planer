$ErrorActionPreference = 'Stop'
$planerRoot = Split-Path -Parent $PSScriptRoot
$planerStagingDir = $null
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
    # Compile away from the synced output folder, then copy the completed executable.
    $planerStagingDir = [IO.Path]::GetFullPath((Join-Path $env:TEMP ('Planer-Installer-' + [guid]::NewGuid().ToString('N'))))
    New-Item -ItemType Directory -Path $planerStagingDir | Out-Null
    & $planerCompiler /Qp ('/O' + $planerStagingDir) installer/Planer.iss
    if ($LASTEXITCODE -ne 0) { throw 'Компиляция установщика завершилась ошибкой' }
    $planerProject = [xml](Get-Content -LiteralPath (Join-Path $planerRoot 'src\Planer.App\Planer.App.csproj') -Raw)
    $planerVersion = [string]$planerProject.Project.PropertyGroup.Version
    if ($planerVersion -notmatch '^\d+\.\d+\.\d+$') { throw 'Не удалось определить версию приложения' }
    $planerInstallerName = "Planer-$planerVersion-win-x64-setup.exe"
    $planerDistDir = Join-Path $planerRoot 'dist'
    New-Item -ItemType Directory -Path $planerDistDir -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $planerStagingDir $planerInstallerName) -Destination (Join-Path $planerDistDir $planerInstallerName) -Force
    Get-Item -LiteralPath (Join-Path $planerDistDir $planerInstallerName) | Select-Object FullName, Length
}
finally {
    if ($planerStagingDir -and (Test-Path -LiteralPath $planerStagingDir)) {
        $planerResolvedStaging = (Resolve-Path -LiteralPath $planerStagingDir).Path
        $planerTempRoot = [IO.Path]::GetFullPath($env:TEMP).TrimEnd('\')
        if ($planerResolvedStaging -ne $planerStagingDir -or !$planerResolvedStaging.StartsWith($planerTempRoot + '\Planer-Installer-', [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Временный путь сборки не прошёл проверку перед очисткой'
        }
        Remove-Item -LiteralPath $planerResolvedStaging -Recurse -Force
    }
    Pop-Location
}
