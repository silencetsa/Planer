using Microsoft.UI.Xaml.Controls;
using Planer_App.Views;
namespace Planer_App;
public sealed partial class MainPage : Page
{
    public MainPage()
    {
        InitializeComponent();
        Surface.Children.Add(new PlannerView(App.ViewModel));
        if (!App.CaptureToday && App.CapturePath is { } path) Loaded += async (_, _) =>
        {
            try
            {
                if (App.CaptureScenario is { } scenario) await UiSnapshot.SaveScenarioAsync(Surface, path, scenario, App.ViewModel);
                else if (App.CaptureTaskPreview) await UiSnapshot.SaveTaskPreviewAsync(Surface, path);
                else await UiSnapshot.SaveAsync(Surface, path);
            }
            catch (Exception ex) { File.WriteAllText(path + ".error.txt", ex.ToString()); }
            App.Window.Close();
        };
    }
    internal void RecoverLayout(bool fallback)
    {
        Surface.Children.Clear();
        Surface.Children.Add(fallback ? Ui.LayoutRecoveryMessage(((App)Microsoft.UI.Xaml.Application.Current).RetryLayout)
            : new PlannerView(App.ViewModel));
    }
}
