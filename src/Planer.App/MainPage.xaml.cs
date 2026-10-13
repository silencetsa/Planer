using Microsoft.UI.Xaml.Controls;
using Planer_App.Views;
namespace Planer_App;
public sealed partial class MainPage : Page
{
    public MainPage()
    {
        InitializeComponent();
        Surface.Children.Add(new PlannerView(App.ViewModel));
    }
}
