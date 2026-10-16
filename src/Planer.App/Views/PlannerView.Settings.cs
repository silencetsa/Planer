using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Planer_App.Views;

public sealed partial class PlannerView
{
    private async void EditSettings(object? sender, EventArgs args)
    {
        if (!OwnsDialogs) return;
        var startup = new ToggleSwitch { IsOn = _vm.AutoStartEnabled, OnContent = "Включён", OffContent = "Выключен" };
        var notifications = new ToggleSwitch { IsOn = _vm.NotificationsEnabled, OnContent = "Включены", OffContent = "Выключены" };
        var error = Ui.Text("", 13, "#CC0A00");
        var body = new StackPanel { Spacing = 12, MinWidth = 350 };
        body.Children.Add(Ui.FieldTitle("Автозапуск")); body.Children.Add(startup);
        body.Children.Add(Ui.Text("Запускать планер в области уведомлений при входе в Windows.", 14));
        body.Children.Add(Ui.FieldTitle("Уведомления")); body.Children.Add(notifications);
        body.Children.Add(Ui.Text("Напоминания работают и при закрытом планере. Утреннее — в 10:00 или при позднем включении до 12:00.", 14));
        body.Children.Add(error);
        var dialog = Ui.Dialog(XamlRoot, "Настройки", body, scale: _scaleHost.ScaleFactor);
        dialog.PrimaryButtonClick += (_, click) =>
        {
            if (_vm.SaveDesktopSettings(startup.IsOn, notifications.IsOn) is { } message) { error.Text = message; click.Cancel = true; }
        };
        await dialog.ShowAsync();
    }
}
