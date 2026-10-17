using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;
using Planer.Core;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace Planer_App;

internal sealed class WindowsNotifications
{
    internal const string Protocol = "planer-desktop";
    private readonly string _appId;
    public WindowsNotifications(string? appId = null) => _appId = appId ?? ShellIdentity.AppUserModelId;
    internal static string Tag(string key) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..16];
    internal static string ActivationArgument(PlannerDestination destination) => destination switch
    { PlannerDestination.Today => "today", PlannerDestination.Habits => "habits", _ => "week" };
    internal static PlannerDestination? ParseActivation(string value) => value switch
    { "today" => PlannerDestination.Today, "week" => PlannerDestination.Week, "habits" => PlannerDestination.Habits, _ => null };
    internal static string Payload(PlannerNotification notification)
    {
        return new XElement("toast", new XAttribute("activationType", "foreground"), new XAttribute("launch", ActivationArgument(notification.Destination)),
            new XElement("visual", new XElement("binding", new XAttribute("template", "ToastGeneric"),
                new XElement("text", "Планер"), new XElement("text", notification.Body)))).ToString(SaveOptions.DisableFormatting);
    }
    public void Deliver(PlannerEngine engine, DateTime now, bool suppressPopup = false)
    {
        engine.CheckNotificationDue(now);
        foreach (var notification in engine.PendingNotifications(now))
        {
            var notifier = ToastNotificationManager.CreateToastNotifier(_appId);
            try { if (notifier.Setting != NotificationSetting.Enabled) return; }
            // A newly registered desktop identity may not have a notification-settings record yet.
            catch (COMException error) when ((uint)error.HResult == 0x80070490) { }
            string tag = Tag(notification.Key);
            // If Windows accepted a toast before a crash, acknowledge it instead of showing a second banner.
            if (!ToastNotificationManager.History.GetHistory(_appId).Any(n => n.Tag == tag && n.Group == "planner"))
            {
                var xml = new XmlDocument(); xml.LoadXml(Payload(notification));
                var toast = new ToastNotification(xml) { Tag = tag, Group = "planner", SuppressPopup = suppressPopup,
                    ExpirationTime = notification.Kind == PlannerNotificationKind.Morning
                        ? new DateTimeOffset(now.Date.AddHours(12)) : DateTimeOffset.Now.AddDays(7) };
                notifier.Show(toast);
            }
            engine.AcknowledgeNotification(notification.Key);
        }
    }
    public void Clear() => ToastNotificationManager.History.Clear(_appId);
    public void UpgradeActivationHistory()
    {
        // Replace existing 1.2.0 entries quietly, preserving tag, text, destination and expiry.
        var notifier = ToastNotificationManager.CreateToastNotifier(_appId);
        foreach (var previous in ToastNotificationManager.History.GetHistory(_appId).Where(t => t.Group == "planner"))
        {
            var content = XDocument.Parse(previous.Content.GetXml());
            if (content.Root?.Attribute("activationType")?.Value != "protocol"
                || ParseDestination(content.Root.Attribute("launch")?.Value ?? "") is not { } destination) continue;
            content.Root.SetAttributeValue("activationType", "foreground");
            content.Root.SetAttributeValue("launch", ActivationArgument(destination));
            content.Root.Element("audio")?.Remove(); content.Root.Add(new XElement("audio", new XAttribute("silent", "true")));
            var xml = new XmlDocument(); xml.LoadXml(content.ToString(SaveOptions.DisableFormatting));
            notifier.Show(new ToastNotification(xml) { Tag = previous.Tag, Group = previous.Group, SuppressPopup = true, ExpirationTime = previous.ExpirationTime });
        }
    }
    internal static PlannerDestination? ParseDestination(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Protocol || uri.Query.Length != 0
            || uri.Fragment.Length != 0 || uri.UserInfo.Length != 0 || !uri.IsDefaultPort || uri.AbsolutePath is not ("" or "/")) return null;
        return uri.Host switch { "today" => PlannerDestination.Today, "week" => PlannerDestination.Week, "habits" => PlannerDestination.Habits, _ => null };
    }
}
