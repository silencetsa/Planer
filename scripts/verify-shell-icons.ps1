param(
    [Parameter(Mandatory=$true)][string]$InstallDirectory,
    [string]$OutputDirectory = ''
)
$ErrorActionPreference = 'Stop'
$planerRoot = Split-Path -Parent $PSScriptRoot
$InstallDirectory = (Resolve-Path -LiteralPath $InstallDirectory).Path
$planerExe = Join-Path $InstallDirectory 'Planer.App.exe'
$planerVersion = ([version](Get-Item -LiteralPath $planerExe).VersionInfo.FileVersion).ToString(3)
$planerIcon = Join-Path $InstallDirectory "Assets\AppIcon-$planerVersion.ico"
foreach ($pair in @(
    @($planerIcon, (Join-Path $planerRoot 'src\Planer.App\Assets\AppIcon.ico')),
    @((Join-Path $InstallDirectory 'Assets\TrayIcon.ico'), (Join-Path $planerRoot 'src\Planer.App\Assets\TrayIcon.ico'))
)) {
    if ((Get-FileHash -LiteralPath $pair[0]).Hash -ne (Get-FileHash -LiteralPath $pair[1]).Hash) {
        throw "Иконка не совпадает с исходником: $($pair[0])"
    }
}
$planerWsh = New-Object -ComObject WScript.Shell
$planerShell = New-Object -ComObject Shell.Application
$planerStart = Join-Path ([Environment]::GetFolderPath('Programs')) 'Планер.lnk'
if (!(Test-Path -LiteralPath $planerStart)) { throw 'Ярлык меню Пуск отсутствует' }
$planerShortcuts = @(@($planerStart, (Join-Path ([Environment]::GetFolderPath('Desktop')) 'Планер.lnk')) |
    Where-Object { Test-Path -LiteralPath $_ })
$planerPinned = Join-Path ([Environment]::GetFolderPath('ApplicationData')) 'Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar'
if (Test-Path -LiteralPath $planerPinned) {
    $planerShortcuts += @(Get-ChildItem -LiteralPath $planerPinned -Filter *.lnk | Where-Object {
        $planerWsh.CreateShortcut($_.FullName).TargetPath -eq $planerExe
    } | ForEach-Object FullName)
}
foreach ($shortcutPath in $planerShortcuts) {
    $shortcut = $planerWsh.CreateShortcut($shortcutPath)
    if ($shortcut.TargetPath -ne $planerExe -or $shortcut.IconLocation -ne ($planerIcon + ',0')) {
        throw "Неверная цель или источник значка: $shortcutPath"
    }
    $item = $planerShell.NameSpace((Split-Path -Parent $shortcutPath)).ParseName([IO.Path]::GetFileName($shortcutPath))
    if ($item.ExtendedProperty('System.AppUserModel.ID') -ne 'Planer.Desktop') {
        throw "Идентификатор ярлыка не совпадает с приложением: $shortcutPath"
    }
    Write-Output "PASS Ярлык: $shortcutPath"
}
if ($OutputDirectory) {
    Add-Type -AssemblyName System.Drawing
    Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class PlannerShellIcon {
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
    public struct FileInfo {
        public IntPtr Icon; public int Index; public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst=260)] public string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst=80)] public string TypeName;
    }
    [DllImport("shell32.dll", CharSet=CharSet.Unicode)]
    public static extern IntPtr SHGetFileInfo(string path, uint attributes, out FileInfo info, uint size, uint flags);
    [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr icon);
}
'@
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    $images = @($planerExe, $planerIcon) + $planerShortcuts
    for ($i = 0; $i -lt $images.Count; $i++) {
        foreach ($sizeFlag in @(0, 1)) {
            $info = New-Object PlannerShellIcon+FileInfo
            $result = [PlannerShellIcon]::SHGetFileInfo($images[$i], 0, [ref]$info, [Runtime.InteropServices.Marshal]::SizeOf($info), (0x100 -bor $sizeFlag))
            if ($result -eq [IntPtr]::Zero -or $info.Icon -eq [IntPtr]::Zero) { throw "Shell не извлекла значок: $($images[$i])" }
            try {
                $icon = [Drawing.Icon]::FromHandle($info.Icon)
                $bitmap = $icon.ToBitmap()
                try { $bitmap.Save((Join-Path $OutputDirectory ("$i-" + $bitmap.Width + '.png')), [Drawing.Imaging.ImageFormat]::Png) }
                finally { $bitmap.Dispose(); $icon.Dispose() }
            } finally { [void][PlannerShellIcon]::DestroyIcon($info.Icon) }
        }
    }
    Write-Output "PASS Изображения значков Shell: $OutputDirectory"
}
Write-Output "PASS Иконки установленной версии $planerVersion"
