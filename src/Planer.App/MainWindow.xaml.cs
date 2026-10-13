using Microsoft.UI.Xaml;
namespace Planer_App;
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true; SetTitleBar(AppTitleBar);
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1400, 850));
        RootFrame.Navigate(typeof(MainPage));
    }
}
