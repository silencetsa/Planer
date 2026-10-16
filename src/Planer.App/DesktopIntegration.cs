using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32;

namespace Planer_App;

public interface IDesktopIntegration
{
    bool AutoStartEnabled { get; }
    void SetAutoStart(bool enabled);
    void SetNotificationChecks(bool enabled);
}

internal sealed class IsolatedDesktopIntegration : IDesktopIntegration
{
    public bool AutoStartEnabled { get; private set; }
    public void SetAutoStart(bool enabled) => AutoStartEnabled = enabled;
    public void SetNotificationChecks(bool enabled) { }
}

internal sealed class DesktopIntegration : IDesktopIntegration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private readonly string _exe;
    private readonly string _taskName;
    internal static string ReminderTaskName => "Planer.Reminders." + WindowsIdentity.GetCurrent().User!.Value;
    public DesktopIntegration(string? exe = null, string? taskName = null)
    {
        _exe = Path.GetFullPath(exe ?? Environment.ProcessPath!); _taskName = taskName ?? ReminderTaskName;
    }
    public bool AutoStartEnabled
    {
        get { using var key = Registry.CurrentUser.OpenSubKey(RunKey); return key?.GetValue(ShellIdentity.AppUserModelId) as string == $"\"{_exe}\" --startup"; }
    }
    public void SetAutoStart(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) key.SetValue(ShellIdentity.AppUserModelId, $"\"{_exe}\" --startup");
        else if (key.GetValue(ShellIdentity.AppUserModelId) is string command && command.StartsWith($"\"{_exe}\"", StringComparison.OrdinalIgnoreCase))
            key.DeleteValue(ShellIdentity.AppUserModelId, throwOnMissingValue: false);
    }
    public void SetNotificationChecks(bool enabled)
    {
        dynamic service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")!)!;
        try
        {
            service.Connect(); dynamic folder = service.GetFolder(@"\");
            try
            {
                dynamic? existing = null;
                try { existing = folder.GetTask(_taskName); }
                catch (Exception ex) when ((uint)ex.HResult is 0x80070002 or 0x8004130F) { }
                if (existing is not null)
                {
                    try
                    {
                        string path = existing.Definition.Actions.Item(1).Path;
                        if (!string.Equals(Path.GetFullPath(path), _exe, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidOperationException("Имя расписания уведомлений уже используется другой установкой планера");
                        if (!enabled) { folder.DeleteTask(_taskName, 0); return; }
                    }
                    finally { Marshal.FinalReleaseComObject(existing); }
                }
                else if (!enabled) return;
                dynamic task = service.NewTask(0);
                try
                {
                    string sid = WindowsIdentity.GetCurrent().User!.Value;
                    task.RegistrationInfo.Description = "Уведомления Планера: 10:00, начало недели и проверка при входе. Окно приложения не открывается.";
                    task.Principal.UserId = sid; task.Principal.LogonType = 3; task.Principal.RunLevel = 0;
                    task.Settings.StartWhenAvailable = true; task.Settings.DisallowStartIfOnBatteries = false;
                    task.Settings.StopIfGoingOnBatteries = false; task.Settings.ExecutionTimeLimit = "PT2M";
                    task.Settings.MultipleInstances = 2; task.Settings.WakeToRun = false;
                    task.Settings.RestartInterval = "PT1M"; task.Settings.RestartCount = 3;
                    dynamic morning = task.Triggers.Create(2);
                    morning.StartBoundary = DateTime.Today.ToString("yyyy-MM-dd'T'10:00:00", System.Globalization.CultureInfo.InvariantCulture); morning.DaysInterval = 1;
                    dynamic monday = task.Triggers.Create(3);
                    monday.StartBoundary = DateTime.Today.ToString("yyyy-MM-dd'T'00:00:00", System.Globalization.CultureInfo.InvariantCulture);
                    monday.WeeksInterval = 1; monday.DaysOfWeek = 2;
                    dynamic login = task.Triggers.Create(9); login.UserId = sid; login.Delay = "PT15S";
                    dynamic action = task.Actions.Create(0); action.Path = _exe; action.Arguments = "--notification-check";
                    action.WorkingDirectory = Path.GetDirectoryName(_exe)!;
                    dynamic registered = folder.RegisterTaskDefinition(_taskName, task, 6, sid, null, 3);
                    Marshal.FinalReleaseComObject(registered);
                }
                finally { Marshal.FinalReleaseComObject(task); }
            }
            finally { Marshal.FinalReleaseComObject(folder); }
        }
        finally { Marshal.FinalReleaseComObject(service); }
    }
}
