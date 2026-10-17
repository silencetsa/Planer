param([string]$InstallDirectory = (Join-Path $env:LOCALAPPDATA 'Programs\Planer'), [switch]$ExpectCold)
$ErrorActionPreference = 'Stop'
$planerRoot = Split-Path -Parent $PSScriptRoot
$planerExe = (Resolve-Path -LiteralPath (Join-Path $InstallDirectory 'Planer.App.exe')).Path
$planerClass = '46D3566D-8F52-45F8-9F20-891FC403878D'
$planerServer = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey("Software\Classes\CLSID\{$planerClass}\LocalServer32")
try {
    if (!$planerServer -or $planerServer.GetValue('') -ne ('"' + $planerExe + '" --toast-activated')) {
        throw 'Native notification server does not point to this installed planner'
    }
} finally { if ($planerServer) { $planerServer.Dispose() } }
function Get-PlannerProcesses {
    @(Get-CimInstance Win32_Process -Filter "Name='Planer.App.exe'" | Where-Object { $_.ExecutablePath -eq $planerExe })
}
$planerBefore = Get-PlannerProcesses
if ($ExpectCold -and $planerBefore.Count) { throw 'Close the planner before the cold activation check' }
$planerLog = Join-Path $env:LOCALAPPDATA 'Planer\desktop.log'
$planerLineCount = if (Test-Path -LiteralPath $planerLog) { @(Get-Content -LiteralPath $planerLog).Count } else { 0 }
$planerClient = Join-Path $planerRoot 'tests\Planer.Windows.Tests\bin\Release\net10.0-windows10.0.26100.0\Planer.Windows.Tests.exe'
if (!(Test-Path -LiteralPath $planerClient)) { throw 'Build Planer.Windows.Tests before running this check' }
$planerProcessId = $null
foreach ($planerTarget in @('today', 'habits', 'week')) {
    $planerStart = [Diagnostics.ProcessStartInfo]::new($planerClient)
    $planerStart.UseShellExecute = $false; $planerStart.CreateNoWindow = $true
    $planerStart.RedirectStandardOutput = $true; $planerStart.RedirectStandardError = $true
    $planerStart.ArgumentList.Add('--activate-installed-toast'); $planerStart.ArgumentList.Add($planerTarget)
    $planerChild = [Diagnostics.Process]::Start($planerStart)
    try {
        if (!$planerChild.WaitForExit(30000)) { $planerChild.Kill(); throw 'Native activation client timed out' }
        $planerOutput = $planerChild.StandardOutput.ReadToEnd(); $planerError = $planerChild.StandardError.ReadToEnd()
        if ($planerChild.ExitCode -ne 0) { throw ("Native activation failed: $planerTarget " + $planerError) }
        $planerReached = $false
        for ($planerAttempt = 0; $planerAttempt -lt 40; $planerAttempt++) {
            $planerNewLines = @(Get-Content -LiteralPath $planerLog -ErrorAction SilentlyContinue | Select-Object -Skip $planerLineCount)
            if ($planerNewLines -match ("Toast activation: " + $planerTarget + '$')) { $planerReached = $true; break }
            Start-Sleep -Milliseconds 100
        }
        if (!$planerReached) { throw "The planner did not confirm navigation: $planerTarget" }
        $planerProcesses = Get-PlannerProcesses
        if ($planerProcesses.Count -ne 1) { throw 'Native activation must use exactly one planner process' }
        if ($planerProcessId -and $planerProcesses[0].ProcessId -ne $planerProcessId) { throw 'Repeated notification launched another planner process' }
        $planerProcessId = $planerProcesses[0].ProcessId
        Write-Output $planerOutput.Trim()
    } finally { $planerChild.Dispose() }
}
Write-Output "All three native notification destinations handled by process $planerProcessId. Cold start: $($planerBefore.Count -eq 0)."
