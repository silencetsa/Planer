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
            try { await UiSnapshot.SaveAsync(Surface, path); }
            catch (Exception ex) { File.WriteAllText(path + ".error.txt", ex.ToString()); }
            App.Window.Close();
        };
    }
}
