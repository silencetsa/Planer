param([Parameter(Mandatory=$true)][string]$Installer)
$ErrorActionPreference = 'Stop'
$planerRoot = Split-Path -Parent $PSScriptRoot
$planerSetup = (Resolve-Path -LiteralPath $Installer).Path
if ([IO.Path]::GetFileName($planerSetup) -notmatch '^Planer-\d+\.\d+\.\d+-win-x64-setup\.exe$') { throw 'Expected the built planner installer' }
$planerLog = Join-Path $planerRoot ('artifacts\install-desktop-' + [guid]::NewGuid().ToString('N') + '.log')
New-Item -ItemType Directory -Path (Split-Path -Parent $planerLog) -Force | Out-Null
$planerTaskName = 'Planer.Install.' + [guid]::NewGuid().ToString('N')
$planerScheduler = New-Object -ComObject Schedule.Service
$planerFolder = $null; $planerRegistered = $false
try {
    $planerScheduler.Connect(); $planerFolder = $planerScheduler.GetFolder('\')
    $planerDefinition = $planerScheduler.NewTask(0)
    $planerDefinition.RegistrationInfo.Description = 'One-time per-user planner update in the desktop session; removed after completion.'
    $planerDefinition.Principal.UserId = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    $planerDefinition.Principal.LogonType = 3; $planerDefinition.Principal.RunLevel = 0
    $planerDefinition.Settings.ExecutionTimeLimit = 'PT5M'
    $planerDefinition.Settings.DisallowStartIfOnBatteries = $false
    $planerDefinition.Settings.StopIfGoingOnBatteries = $false
    $planerAction = $planerDefinition.Actions.Create(0)
    $planerAction.Path = $planerSetup; $planerAction.WorkingDirectory = Split-Path -Parent $planerSetup
    $planerAction.Arguments = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS /TASKS= /LOG="' + $planerLog + '"'
    # InteractiveToken launches in the real desktop registry context; no password or elevation.
    $planerTask = $planerFolder.RegisterTaskDefinition($planerTaskName, $planerDefinition, 6, $planerDefinition.Principal.UserId, $null, 3)
    $planerRegistered = $true
    $planerTask.Run($null) | Out-Null
    $planerDeadline = [DateTime]::UtcNow.AddMinutes(4)
    do {
        Start-Sleep -Milliseconds 500
        # IRunningTask.Refresh throws after normal completion; read the persistent task instead.
        $planerTask = $planerFolder.GetTask($planerTaskName)
        $planerTaskState = $planerTask.State
        if ([DateTime]::UtcNow -gt $planerDeadline) { throw 'Installer is still running; inspect its log before retrying' }
    } while ($planerTaskState -in @(2,4))
    $planerTask = $planerFolder.GetTask($planerTaskName)
    if ($planerTask.LastTaskResult -ne 0) { throw ('Installer failed: ' + $planerTask.LastTaskResult + '. Log: ' + $planerLog) }
    Write-Output ('Desktop-session installation completed. Log: ' + $planerLog)
} finally {
    if ($planerRegistered) { $planerFolder.DeleteTask($planerTaskName, 0) }
    if ($planerFolder) { [Runtime.InteropServices.Marshal]::FinalReleaseComObject($planerFolder) | Out-Null }
    [Runtime.InteropServices.Marshal]::FinalReleaseComObject($planerScheduler) | Out-Null
}
