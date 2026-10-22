param([string]$Installer = '')
$ErrorActionPreference = 'Stop'
$planerRoot = Split-Path -Parent $PSScriptRoot
if (!$Installer) { $Installer = Join-Path $planerRoot 'dist\Planer-1.3.0-win-x64-setup.exe' }
$Installer = (Resolve-Path -LiteralPath $Installer).Path
$planerUninstallKeyName = '{A316F590-BB3F-43E1-81C8-861EA1BC03BA}_is1'
$planerRegistryPaths = @(
    "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$planerUninstallKeyName",
    "HKCU:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\$planerUninstallKeyName",
    "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$planerUninstallKeyName",
    "HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\$planerUninstallKeyName"
)
if (@($planerRegistryPaths | Where-Object { Test-Path -LiteralPath $_ }).Count) {
    throw 'Планер уже установлен. Проверка остановлена, чтобы не изменять существующую установку.'
}
$planerArtifactRoot = (Resolve-Path -LiteralPath (Join-Path $planerRoot 'artifacts')).Path
$planerTestRoot = [IO.Path]::GetFullPath((Join-Path $planerArtifactRoot ('installer-smoke-' + [guid]::NewGuid().ToString('N'))))
if (!$planerTestRoot.StartsWith($planerArtifactRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Тестовая папка вышла за пределы artifacts'
}
New-Item -ItemType Directory -Path $planerTestRoot | Out-Null
$planerInstallDir = Join-Path $planerTestRoot 'app'
$planerDataDir = Join-Path $planerTestRoot 'data'
$planerImagePath = Join-Path $planerTestRoot 'installed-week.png'
$planerRegistryVerified = $false
try {
    $planerSetupArgs = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/NOICONS', '/TASKS=',
        ('/DIR="' + $planerInstallDir + '"'), ('/LOG="' + (Join-Path $planerTestRoot 'install.log') + '"'))
    $planerSetupProcess = Start-Process -FilePath $Installer -ArgumentList $planerSetupArgs -WindowStyle Hidden -PassThru
    if (!$planerSetupProcess.WaitForExit(45000)) { throw 'Установка не завершилась за 45 секунд' }
    if ($planerSetupProcess.ExitCode -ne 0) { throw "Ошибка установки: $($planerSetupProcess.ExitCode)" }
    $planerInstalledKey = @($planerRegistryPaths | Where-Object { Test-Path -LiteralPath $_ })
    if ($planerInstalledKey.Count -ne 1) { throw 'Ожидалась одна тестовая запись установленного приложения' }
    $planerRegisteredDir = [IO.Path]::GetFullPath((Get-ItemProperty -LiteralPath $planerInstalledKey[0]).InstallLocation).TrimEnd('\')
    if ($planerRegisteredDir -ne $planerInstallDir) { throw 'Путь в реестре не совпадает с изолированной тестовой папкой' }
    $planerRegistryVerified = $true
    $planerInstalledExe = (Resolve-Path -LiteralPath (Join-Path $planerInstallDir 'Planer.App.exe')).Path
    $planerPublishedExe = Join-Path $planerRoot 'artifacts\publish\win-x64\Planer.App.exe'
    if ((Get-FileHash -LiteralPath $planerInstalledExe).Hash -ne (Get-FileHash -LiteralPath $planerPublishedExe).Hash) { throw 'Исполняемый файл отличается от опубликованного' }
    $planerLaunchArgs = @('--data-dir', ('"' + $planerDataDir + '"'), '--demo', '--view', 'Week', '--capture', ('"' + $planerImagePath + '"'))
    $planerAppProcess = Start-Process -FilePath $planerInstalledExe -ArgumentList $planerLaunchArgs -WorkingDirectory $planerInstallDir -WindowStyle Hidden -PassThru
    if (!$planerAppProcess.WaitForExit(15000)) {
        Stop-Process -Id $planerAppProcess.Id -Force -ErrorAction SilentlyContinue
        throw 'Установленное приложение не завершило тестовый рендер'
    }
    if (Test-Path -LiteralPath ($planerImagePath + '.error.txt')) { throw (Get-Content -LiteralPath ($planerImagePath + '.error.txt') -Raw) }
    if (!(Test-Path -LiteralPath $planerImagePath)) { throw 'Установленное приложение не создало рендер недели' }
    Write-Output "PASS Установка и запуск: $planerImagePath"
}
finally {
    if ($planerRegistryVerified) {
        # Uninstall only the freshly installed, verified directory inside this workspace.
        $planerResolvedInstallDir = (Resolve-Path -LiteralPath $planerInstallDir).Path.TrimEnd('\')
        $planerRegisteredDir = [IO.Path]::GetFullPath((Get-ItemProperty -LiteralPath $planerInstalledKey[0]).InstallLocation).TrimEnd('\')
        if ($planerResolvedInstallDir -ne $planerInstallDir -or $planerRegisteredDir -ne $planerInstallDir -or
            !$planerResolvedInstallDir.StartsWith($planerTestRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Безопасная проверка пути перед тестовым удалением не пройдена'
        }
        $planerUninstaller = Join-Path $planerResolvedInstallDir 'unins000.exe'
        $planerUninstallProcess = Start-Process -FilePath $planerUninstaller -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART',
            ('/LOG="' + (Join-Path $planerTestRoot 'uninstall.log') + '"')) -WorkingDirectory $planerTestRoot -WindowStyle Hidden -PassThru
        if (!$planerUninstallProcess.WaitForExit(45000) -or $planerUninstallProcess.ExitCode -ne 0) { throw 'Тестовое удаление приложения не завершилось успешно' }
        if ((Test-Path -LiteralPath $planerInstalledExe) -or (Test-Path -LiteralPath $planerInstalledKey[0])) { throw 'Тестовая установка не была удалена полностью' }
        if (!(Test-Path -LiteralPath (Join-Path $planerDataDir 'planner.db'))) { throw 'Удаление приложения затронуло отдельную базу данных' }
        Write-Output 'PASS Удаление приложения, отдельная база сохранена'
    }
}
