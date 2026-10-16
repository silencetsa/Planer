param([string]$Executable = '', [string[]]$Only = @())
$ErrorActionPreference = 'Stop'
$planerRoot = Split-Path -Parent $PSScriptRoot
if (!$Executable) { $Executable = Join-Path $planerRoot 'src\Planer.App\bin\x64\Release\net10.0-windows10.0.26100.0\win-x64\Planer.App.exe' }
$Executable = (Resolve-Path -LiteralPath $Executable).Path
$planerQaRunDir = Join-Path $planerRoot ('artifacts\qa-runs\' + [guid]::NewGuid().ToString('N'))
$planerDataDir = Join-Path $planerQaRunDir 'demo'
$planerImageDir = Join-Path $planerRoot 'artifacts\screenshots'
New-Item -ItemType Directory -Path $planerImageDir -Force | Out-Null
$planerCases = @(
    @{ Name='week'; View='Week' }, @{ Name='month'; View='Month' },
    @{ Name='year'; View='Year' }, @{ Name='habits'; View='Habits' },
    @{ Name='payments'; View='Payments' }, @{ Name='today'; View='Today' },
    @{ Name='habits-actions'; View='Habits'; Scenario='HabitsActions' },
    @{ Name='payments-actions'; View='Payments'; Scenario='PaymentsActions' },
    @{ Name='payments-actions-large'; View='Payments'; Scenario='PaymentsActions'; Large=$true },
    @{ Name='payments-actions-completed'; View='Payments'; Scenario='PaymentsActionsCompleted'; Separate=$true },
    @{ Name='layout-stress'; View='Week'; Scenario='LayoutStress'; Separate=$true; Timeout=45000 },
    @{ Name='today-layout-stress'; View='Today'; Scenario='TodayLayoutStress'; Separate=$true; Timeout=30000 },
    @{ Name='layout-recovery'; View='Week'; Scenario='LayoutRecovery'; Separate=$true; Timeout=30000 },
    @{ Name='tray-lifecycle'; View='Week'; Scenario='TrayLifecycle' },
    @{ Name='recurring-month'; View='Month'; Scenario='RecurringMonth'; Separate=$true },
    @{ Name='week-large'; View='Week'; Large=$true },
    @{ Name='year-large'; View='Year'; Large=$true },
    @{ Name='year-mood'; View='Year'; Scenario='YearMood' },
    @{ Name='year-habits'; View='Year'; Scenario='YearHabits' },
    @{ Name='week-notes'; View='Week'; Scenario='Notes' },
    @{ Name='month-notes'; View='Month'; Scenario='Notes' },
    @{ Name='day-plus'; View='Week'; Scenario='DayPlus' },
    @{ Name='period-calendar'; View='Week'; Scenario='Calendar' },
    @{ Name='period-calendar-large'; View='Week'; Scenario='Calendar'; Large=$true },
    @{ Name='period-calendar-navigation'; View='Week'; Scenario='CalendarNavigate' },
    @{ Name='calendar-open-year'; View='Week'; Scenario='CalendarOpenYear' },
    @{ Name='calendar-open-month'; View='Week'; Scenario='CalendarOpenMonth' },
    @{ Name='calendar-open-week'; View='Week'; Scenario='CalendarOpenWeek' },
    @{ Name='payment-normal'; View='Week'; Scenario='PaymentNormal' },
    @{ Name='payment-hover'; View='Week'; Scenario='PaymentHover' },
    @{ Name='payment-done'; View='Week'; Scenario='PaymentDone'; Separate=$true },
    @{ Name='completed-task-colors'; View='Week'; Scenario='CompletedTaskColors'; Separate=$true },
    @{ Name='task-dialog'; View='Week'; Scenario='TaskDialog' },
    @{ Name='task-dialog-on'; View='Week'; Scenario='TaskDialogOn' },
    @{ Name='task-dialog-large'; View='Week'; Scenario='TaskDialog'; Large=$true },
    @{ Name='task-multiline-dialog'; View='Week'; Scenario='TaskMultilineDialog'; Separate=$true },
    @{ Name='delete-task-dialog'; View='Week'; Scenario='DeleteTaskDialog' },
    @{ Name='delete-recurring-dialog'; View='Week'; Scenario='DeleteRecurringDialog'; Separate=$true },
    @{ Name='move-recurring-dialog'; View='Week'; Scenario='MoveRecurringDialog'; Separate=$true },
    @{ Name='habit-dialog'; View='Habits'; Scenario='HabitDialog'; Empty=$true },
    @{ Name='payment-dialog'; View='Payments'; Scenario='PaymentDialog' },
    @{ Name='finish-habit-dialog'; View='Habits'; Scenario='FinishHabitDialog' },
    @{ Name='task-preview'; View='Week'; Preview=$true },
    @{ Name='task-preview-large'; View='Week'; Preview=$true; Large=$true }
)
if ($Only.Count -gt 0) {
    $planerCases = @($planerCases | Where-Object { $_.Name -in $Only })
    if ($planerCases.Count -ne @($Only | Select-Object -Unique).Count) { throw 'Указан неизвестный сценарий QA' }
}
foreach ($planerCase in $planerCases) {
    $planerScreen = $planerCase.Name
    $planerImagePath = Join-Path $planerImageDir ($planerScreen + '.png')
    $planerErrorPath = $planerImagePath + '.error.txt'
    if (Test-Path -LiteralPath $planerErrorPath) { Remove-Item -LiteralPath $planerErrorPath }
    if (Test-Path -LiteralPath $planerImagePath) { Remove-Item -LiteralPath $planerImagePath }
    $planerCaseData = if ($planerCase.Empty) { Join-Path $planerQaRunDir 'empty' } elseif ($planerCase.Separate) { Join-Path $planerQaRunDir $planerScreen } else { $planerDataDir }
    $planerArgs = @('--data-dir', ('"' + $planerCaseData + '"'), '--view', $planerCase.View, '--capture', ('"' + $planerImagePath + '"'))
    if (!$planerCase.Empty) { $planerArgs += '--demo' }
    if ($planerCase.Scenario) { $planerArgs += @('--scenario', $planerCase.Scenario) }
    if ($planerCase.Large) { $planerArgs += '--capture-large' }
    if ($planerCase.Preview) { $planerArgs += '--capture-task-preview' }
    $planerProcess = Start-Process -FilePath $Executable -ArgumentList $planerArgs -WorkingDirectory (Split-Path -Parent $Executable) -WindowStyle Hidden -PassThru
    $planerTimeout = if ($planerCase.Timeout) { $planerCase.Timeout } else { 15000 }
    if (!$planerProcess.WaitForExit($planerTimeout)) {
        Stop-Process -Id $planerProcess.Id -Force -ErrorAction SilentlyContinue
        throw "Рендер $planerScreen не завершился за $($planerTimeout / 1000) секунд"
    }
    if (Test-Path -LiteralPath $planerErrorPath) { throw (Get-Content -LiteralPath $planerErrorPath -Raw) }
    if (!(Test-Path -LiteralPath $planerImagePath)) { throw "Рендер $planerScreen не создан (код $($planerProcess.ExitCode)); проверьте, не открыта ли QA-база другим процессом" }
    Get-Item -LiteralPath $planerImagePath | Select-Object Name, Length
}
