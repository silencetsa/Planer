using System.ComponentModel;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Planer_App.ViewModels;
using Planer_App.Views;

namespace Planer_App;

public sealed partial class TodayWindow : Window
{
    private readonly PlannerViewModel _vm;
    public TodayWindow(PlannerViewModel vm)
    {
        _vm = vm; InitializeComponent(); ExtendsContentIntoTitleBar = true; SetTitleBar(DayTitleBar);
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true; presenter.IsMaximizable = false; presenter.IsMinimizable = false;
        }
        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest).WorkArea;
        AppWindow.Resize(new Windows.Graphics.SizeInt32(340, Math.Min(610, area.Height - 50)));
        AppWindow.Move(new Windows.Graphics.PointInt32(area.X + area.Width - 364, area.Y + 24));
        DaySurface.Children.Add(new PlannerView(vm, floating: true)); RefreshTitle();
        vm.PropertyChanged += Changed;
        Activated += (_, e) => { if (e.WindowActivationState != WindowActivationState.Deactivated) App.FloatingOwnsDialogs = true; };
        Closed += (_, _) => vm.PropertyChanged -= Changed;
    }
    private void Changed(object? sender, PropertyChangedEventArgs args) { if (args.PropertyName == nameof(PlannerViewModel.Revision)) RefreshTitle(); }
    private void RefreshTitle() { Title = $"Сегодня · {_vm.Today.ToString("d MMMM", PlannerViewModel.Russian)}"; DayTitleBar.Title = Title; }
}
