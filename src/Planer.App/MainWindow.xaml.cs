using Microsoft.UI.Xaml;
namespace Planer_App;
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true; SetTitleBar(AppTitleBar);
        AppWindow.SetIcon(ShellIdentity.IconPath);
        var area = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(AppWindow.Id, Microsoft.UI.Windowing.DisplayAreaFallback.Nearest).WorkArea;
        int width = Math.Min(1400, area.Width - 24), height = Math.Min(850, area.Height - 24);
        AppWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
        AppWindow.Move(new Windows.Graphics.PointInt32(area.X + (area.Width - width) / 2, area.Y + (area.Height - height) / 2));
        WindowSizing.Configure(this, floating: false);
        RootFrame.Navigate(typeof(MainPage));
    }
    internal void RecoverLayout(bool fallback)
    {
        WindowSizing.RestoreMinimum(this, floating: false);
        if (RootFrame.Content is MainPage page) page.RecoverLayout(fallback);
    }
}
