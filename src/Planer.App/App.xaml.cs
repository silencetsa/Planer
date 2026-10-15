using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Planer.Core;
using Planer.Data;
using Planer_App.ViewModels;

namespace Planer_App;

public partial class App : Application
{
    public static Window Window { get; private set; } = null!;
    public static PlannerViewModel ViewModel { get; private set; } = null!;
    public static bool FloatingOwnsDialogs { get; set; }
    public static string DatabasePath { get; private set; } = "";
    internal static string? CapturePath { get; private set; }
    internal static bool CaptureToday { get; private set; }
    internal static bool CaptureTaskPreview { get; private set; }
    internal static string? CaptureScenario { get; private set; }
    internal static string? DragTracePath { get; private set; }
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _clockTimer;
    private Mutex? _instanceMutex;
    private TodayWindow? _dayWindow;
    private TrayIcon? _tray;
    internal static TrayIcon? Tray => (Current as App)?._tray;
    internal static TodayWindow? FloatingWindow => (Current as App)?._dayWindow;
    public App() { InitializeComponent(); }
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var commandLine = Environment.GetCommandLineArgs();
        var dataIndex = Array.IndexOf(commandLine, "--data-dir");
        var dataDir = dataIndex >= 0 && dataIndex + 1 < commandLine.Length ? commandLine[dataIndex + 1]
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Planer");
        DatabasePath = Path.Combine(Path.GetFullPath(dataDir), "planner.db");
        var mutexKey = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(DatabasePath.ToUpperInvariant())))[..24];
        _instanceMutex = new Mutex(true, "Local\\Planer-" + mutexKey, out var firstInstance);
        if (!firstInstance) { Exit(); return; }
        var existed = File.Exists(DatabasePath);
        int captureIndex = Array.IndexOf(commandLine, "--capture");
        if (dataIndex >= 0 && captureIndex >= 0 && captureIndex + 1 < commandLine.Length) CapturePath = Path.GetFullPath(commandLine[captureIndex + 1]);
        CaptureTaskPreview = CapturePath is not null && commandLine.Contains("--capture-task-preview");
        if (dataIndex >= 0 && commandLine.Contains("--trace-drag")) DragTracePath = Path.Combine(Path.GetFullPath(dataDir), "drag-trace.jsonl");
        int scenarioIndex = Array.IndexOf(commandLine, "--scenario");
        if (CapturePath is not null && scenarioIndex >= 0 && scenarioIndex + 1 < commandLine.Length) CaptureScenario = commandLine[scenarioIndex + 1];
        try
        {
            var store = new SqlitePlannerStore(DatabasePath);
            if (!existed && commandLine.Contains("--demo") && dataIndex >= 0) store.Save(DemoData.Create(DateOnly.FromDateTime(DateTime.Now)));
            ViewModel = new PlannerViewModel(new PlannerEngine(store, new SystemPlannerClock()));
            int viewIndex = Array.IndexOf(commandLine, "--view");
            if (viewIndex >= 0 && viewIndex + 1 < commandLine.Length && Enum.TryParse<PlannerScreen>(commandLine[viewIndex + 1], true, out var requestedScreen))
                ViewModel.Navigate(requestedScreen, ViewModel.Today);
            CaptureToday = CapturePath is not null && viewIndex >= 0 && viewIndex + 1 < commandLine.Length
                && string.Equals(commandLine[viewIndex + 1], "Today", StringComparison.OrdinalIgnoreCase);
            Window = new MainWindow();
            ViewModel.FloatRequested += (_, _) =>
            {
                if (_dayWindow is null)
                {
                    _dayWindow = new TodayWindow(ViewModel);
                    _dayWindow.Closed += (_, _) => { _dayWindow = null; FloatingOwnsDialogs = false; };
                }
                _dayWindow.Activate();
            };
            Window.Activated += (_, e) => { if (e.WindowActivationState != WindowActivationState.Deactivated) FloatingOwnsDialogs = false; };
            _clockTimer = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().CreateTimer();
            _clockTimer.Interval = TimeSpan.FromSeconds(30); _clockTimer.Tick += (_, _) => ViewModel.Run(ViewModel.Tick); _clockTimer.Start();
            Window.Closed += (_, _) => { _clockTimer.Stop(); _tray?.Dispose(); _dayWindow?.Close(); ViewModel.Dispose(); _instanceMutex.Dispose(); };
            UnhandledException += (_, e) => { try { File.AppendAllText(Path.Combine(dataDir, "crash.log"), $"{DateTime.Now:O} {e.Exception}\n"); } catch { } };
        }
        catch (Exception ex)
        {
            try { Directory.CreateDirectory(dataDir); File.AppendAllText(Path.Combine(dataDir, "crash.log"), $"{DateTime.Now:O} {ex}\n"); } catch { }
            if (CapturePath is not null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CapturePath)!);
                File.WriteAllText(CapturePath + ".error.txt", ex.ToString());
                Exit(); return;
            }
            Window = new Window { Title = "Планер — ошибка запуска", Content = new Grid
            {
                RequestedTheme = ElementTheme.Light,
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White),
                Children = { new TextBlock { Text = "Не удалось запустить планер.\n" + DatabasePath + "\n\n" + ex.Message,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Black), TextWrapping = TextWrapping.Wrap, Margin = new(24) } }
            } };
        }
        Window.Activate();
        // Ordinary sessions have one icon; bulk rendering does not populate the user's tray.
        if (Window is MainWindow && _clockTimer is not null && (CapturePath is null || CaptureScenario == "TrayLifecycle"))
        {
            void ReportTrayError(Exception error)
            {
                try { File.AppendAllText(Path.Combine(dataDir, "crash.log"), $"{DateTime.Now:O} Область уведомлений: {error}\n"); } catch { }
            }
            try { _tray = new TrayIcon(Window, ViewModel, ReportTrayError); }
            catch (Exception error) { ReportTrayError(error); }
        }
        if (CapturePath is not null)
        {
            if (commandLine.Contains("--capture-large")) Window.AppWindow.Resize(new Windows.Graphics.SizeInt32(1920, 1080));
            Window.AppWindow.Move(new Windows.Graphics.PointInt32(-3000, -3000));
        }
        if (CaptureToday) ViewModel.FloatCommand.Execute(null);
    }
}
