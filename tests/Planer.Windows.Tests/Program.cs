using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Win32;
using Planer.Core;
using Planer_App;
using Windows.UI.Notifications;

if (args.Contains("--notification-check")) return 0; // A temporary Task Scheduler action must never recursively run the test suite.
int passed = 0, failed = 0;
async Task Test(string name, Func<Task> test)
{
    try { await test(); Console.WriteLine("PASS " + name); passed++; }
    catch (Exception error) { Console.WriteLine("FAIL " + name + ": " + error); failed++; }
}
void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

await Test("Protocol activation accepts only the three planner destinations", () =>
{
    foreach (var (host, destination) in new[] { ("today", PlannerDestination.Today), ("week", PlannerDestination.Week), ("habits", PlannerDestination.Habits) })
        Assert(WindowsNotifications.ParseDestination("planer-desktop://" + host) == destination, "Valid destination rejected");
    foreach (var invalid in new[] { "https://today", "planer-desktop://today/path", "planer-desktop://today?file=x", "planer-desktop://week#x", "planer-desktop://u@today", "planer-desktop://today:123", "planer-desktop://unknown" })
        Assert(WindowsNotifications.ParseDestination(invalid) is null, "Unsafe activation accepted: " + invalid);
    var xml = XDocument.Parse(WindowsNotifications.Payload(new() { Body = "Привычка <&> выполняется! <3", Destination = PlannerDestination.Today }));
    Assert(xml.Descendants("text").Last().Value == "Привычка <&> выполняется! <3", "Notification text was not XML-escaped");
    Assert((string?)xml.Root!.Attribute("activationType") == "protocol", "Notification must use protocol activation");
    return Task.CompletedTask;
});
await Test("Current-user activation pipe delivers only fixed commands and confirms processing", async () =>
{
    string key = Guid.NewGuid().ToString("N"); var received = new List<DesktopActivation>();
    using var server = new ActivationBridge(key, activation => { received.Add(activation); return Task.CompletedTask; });
    foreach (var activation in Enum.GetValues<DesktopActivation>()) Assert(await ActivationBridge.Forward(key, activation), "Pipe did not acknowledge activation");
    Assert(received.SequenceEqual(Enum.GetValues<DesktopActivation>()), "Commands were lost or changed");
});
await Test("Autostart changes only the unique test Run value and round-trips quoted paths", () =>
{
    var integration = new DesktopIntegration();
    try
    {
        Assert(!integration.AutoStartEnabled, "Unique test autostart already exists");
        integration.SetAutoStart(true); Assert(integration.AutoStartEnabled, "Autostart was not enabled");
        integration.SetAutoStart(false); Assert(!integration.AutoStartEnabled, "Autostart was not removed");
    }
    finally { integration.SetAutoStart(false); }
    return Task.CompletedTask;
});
await Test("A per-user reminder task uses local 10:00, Monday midnight and login without elevation", () =>
{
    string name = "Planer.QA." + Guid.NewGuid().ToString("N"); var integration = new DesktopIntegration(taskName: name);
    dynamic service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")!)!;
    try
    {
        integration.SetNotificationChecks(true); service.Connect(); dynamic folder = service.GetFolder(@"\");
        try
        {
            dynamic task = folder.GetTask(name);
            try
            {
                var xml = XDocument.Parse((string)task.Xml); XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
                Assert(xml.Descendants(ns + "StartBoundary").Any(n => n.Value.EndsWith("T10:00:00")), "10:00 trigger is absent");
                Assert(xml.Descendants(ns + "Monday").Any(), "Monday trigger is absent");
                Assert(xml.Descendants(ns + "LogonTrigger").Any(), "Login catch-up trigger is absent");
                Assert((int)task.Definition.Principal.RunLevel == 0, "Task requests elevation");
                Assert(xml.Descendants(ns + "StartWhenAvailable").Single().Value == "true", "Missed runs are dropped");
                Assert(xml.Descendants(ns + "Arguments").Single().Value == "--notification-check", "Task starts a visible planner");
            }
            finally { Marshal.FinalReleaseComObject(task); }
        }
        finally { Marshal.FinalReleaseComObject(folder); }
        integration.SetNotificationChecks(false); integration.SetNotificationChecks(false);
    }
    finally { integration.SetNotificationChecks(false); Marshal.FinalReleaseComObject(service); }
    return Task.CompletedTask;
});
await Test("Windows accepts a quiet toast with the planner payload and preserves it in its history", async () =>
{
    string id = ShellIdentity.AppUserModelId; string keyPath = @"Software\Classes\AppUserModelId\" + id;
    using (var key = Registry.CurrentUser.CreateSubKey(keyPath))
    {
        key.SetValue("DisplayName", "Планер — проверка");
        key.SetValue("CustomActivator", "{46D3566D-8F52-45F8-9F20-891FC403878D}");
    }
    var gateway = new WindowsNotifications(id); var store = new TestStore(); var engine = new PlannerEngine(store, new TestClock());
    using var shortcut = new TestShortcut(id);
    var notification = new PlannerNotification { Key = "qa:" + Guid.NewGuid(), Body = "Проверка уведомления <3", Kind = PlannerNotificationKind.Rollover,
        Created = engine.Today, Destination = PlannerDestination.Week };
    engine.State.Notifications.Add(notification); store.Save(engine.State);
    try
    {
        gateway.Deliver(engine, DateTime.Now, suppressPopup: true);
        IReadOnlyList<ToastNotification> history = [];
        for (int i = 0; i < 15; i++)
        {
            history = ToastNotificationManager.History.GetHistory(id);
            if (history.Any(n => n.Tag == WindowsNotifications.Tag(notification.Key))) break;
            await Task.Delay(100);
        }
        Assert(history.Any(n => n.Tag == WindowsNotifications.Tag(notification.Key)), "Windows did not persist the toast");
        Assert(engine.State.Notifications.Single(n => n.Key == notification.Key).Dispatched, "Delivery receipt was not saved");
        gateway.Deliver(engine, DateTime.Now, suppressPopup: true);
        Assert(ToastNotificationManager.History.GetHistory(id).Count == history.Count, "The same notification was duplicated");
    }
    finally { try { gateway.Clear(); } finally { Registry.CurrentUser.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false); } }
});
Console.WriteLine($"{passed} passed, {failed} failed"); return failed == 0 ? 0 : 1;

sealed class TestClock : IPlannerClock { public DateOnly Today => DateOnly.FromDateTime(DateTime.Now); }
sealed class TestStore : IPlannerStore
{
    private string _json = JsonSerializer.Serialize(new PlannerState());
    public PlannerState Load() => JsonSerializer.Deserialize<PlannerState>(_json)!;
    public void Save(PlannerState state) => _json = JsonSerializer.Serialize(state);
}
namespace Planer_App
{
    // The linked production adapters use an isolated identity in this executable, never the user's planner identity.
    internal static class ShellIdentity { internal static readonly string AppUserModelId = "Planer.Desktop.QA." + Guid.NewGuid().ToString("N"); }
}
