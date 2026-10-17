using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Win32;
using Planer.Core;
using Planer_App;
using Windows.UI.Notifications;

if (args.Contains("--notification-check")) return 0; // A temporary Task Scheduler action must never recursively run the test suite.
if (args.Length == 2 && args[0] == "--activate-installed-toast")
{
    if (WindowsNotifications.ParseActivation(args[1]) is null) return 2;
    object callback = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid(ToastActivationBridge.ClassIdText))!)!;
    try { Marshal.ThrowExceptionForHR(((IToastActivationCallback)callback).Activate("Planer.Desktop", args[1], 0, 0)); }
    finally { Marshal.FinalReleaseComObject(callback); }
    Console.WriteLine("Native toast activation accepted: " + args[1]); return 0;
}
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
    Assert((string?)xml.Root!.Attribute("activationType") == "foreground", "Notification must use the native activator");
    foreach (var destination in Enum.GetValues<PlannerDestination>())
    {
        var content = XDocument.Parse(WindowsNotifications.Payload(new() { Destination = destination }));
        Assert(WindowsNotifications.ParseActivation(content.Root!.Attribute("launch")!.Value) == destination, "Notification destination was lost");
    }
    foreach (string invalid in new[] { "", "--startup", "today/path", "week?file=x", "planer-desktop://today", "Today" })
        Assert(WindowsNotifications.ParseActivation(invalid) is null, "Unexpected native activation argument accepted");
    return Task.CompletedTask;
});
await Test("Windows COM resolves the native toast callback and routes all destinations without URI activation", async () =>
{
    var received = new System.Collections.Concurrent.ConcurrentQueue<PlannerDestination>();
    var classId = Guid.NewGuid(); string appId = "Planer.Desktop.QA." + Guid.NewGuid().ToString("N");
    using var bridge = new ToastActivationBridge(destination => { received.Enqueue(destination); return true; }, classId, appId);
    await Task.Run(() =>
    {
        object native = Activator.CreateInstance(Type.GetTypeFromCLSID(classId)!)!;
        try
        {
            var callback = (IToastActivationCallback)native;
            foreach (var destination in Enum.GetValues<PlannerDestination>())
                Marshal.ThrowExceptionForHR(callback.Activate(appId, WindowsNotifications.ActivationArgument(destination), 0, 0));
            Assert(callback.Activate("Other.App", "today", 0, 0) < 0, "Unrelated app identity accepted");
            Assert(callback.Activate(appId, "today?path=x", 0, 0) < 0, "Arbitrary activation command accepted");
            Assert(callback.Activate(appId, "today", 0, 1) < 0, "Unexpected user input accepted");
        }
        finally { Marshal.FinalReleaseComObject(native); }
    });
    Assert(received.SequenceEqual(Enum.GetValues<PlannerDestination>()), "Native COM destinations were changed or duplicated");
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
        foreach (var destination in Enum.GetValues<PlannerDestination>())
        {
            var legacy = XDocument.Parse(WindowsNotifications.Payload(new() { Body = "Сохранённое уведомление <3", Destination = destination }));
            legacy.Root!.SetAttributeValue("activationType", "protocol");
            legacy.Root.SetAttributeValue("launch", "planer-desktop://" + WindowsNotifications.ActivationArgument(destination));
            var xml = new Windows.Data.Xml.Dom.XmlDocument(); xml.LoadXml(legacy.ToString());
            ToastNotificationManager.CreateToastNotifier(id).Show(new ToastNotification(xml) { Tag = "legacy-" + destination, Group = "planner", SuppressPopup = true, ExpirationTime = DateTimeOffset.Now.AddHours(1) });
        }
        for (int i = 0; i < 20 && ToastNotificationManager.History.GetHistory(id).Count < history.Count + 3; i++) await Task.Delay(100);
        gateway.UpgradeActivationHistory();
        for (int i = 0; i < 20; i++)
        {
            if (ToastNotificationManager.History.GetHistory(id).All(n => XDocument.Parse(n.Content.GetXml()).Root!.Attribute("activationType")!.Value == "foreground")) break;
            await Task.Delay(100);
        }
        var upgraded = ToastNotificationManager.History.GetHistory(id).Where(n => n.Tag.StartsWith("legacy-")).ToList();
        Assert(upgraded.Count == 3, "Existing notifications were lost on upgrade");
        foreach (var existing in upgraded)
        {
            var xml = XDocument.Parse(existing.Content.GetXml());
            Assert(xml.Root!.Attribute("activationType")!.Value == "foreground", "Old notification still uses URI activation");
            Assert(WindowsNotifications.ParseActivation(xml.Root.Attribute("launch")!.Value) is { } destination && existing.Tag == "legacy-" + destination, "Existing notification changed destination");
            Assert(xml.Descendants("text").Last().Value == "Сохранённое уведомление <3", "Existing notification text changed");
            Assert(existing.ExpirationTime.HasValue, "Existing notification expiry was lost");
        }
        gateway.UpgradeActivationHistory();
        Assert(ToastNotificationManager.History.GetHistory(id).Count == history.Count + 3, "Upgrade duplicated existing notifications");
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
