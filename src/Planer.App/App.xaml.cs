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
    private PlannerEngine? _engine;
    private ActivationBridge? _activationBridge;
    private bool _isolated, _checkingNotifications;
    internal static TrayIcon? Tray => (Current as App)?._tray;
    internal static TodayWindow? FloatingWindow => (Current as App)?._dayWindow;
    public App() { UnhandledException += OnUnhandledException; InitializeComponent(); }
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var commandLine = Environment.GetCommandLineArgs();
        int protocolIndex = Array.IndexOf(commandLine, "--protocol");
        PlannerDestination? destination = null;
        if (protocolIndex >= 0)
        {
            if (protocolIndex != 1 || commandLine.Length != 3 || (destination = WindowsNotifications.ParseDestination(commandLine[2])) is null)
            { Environment.ExitCode = 2; Exit(); return; }
        }
        var dataIndex = Array.IndexOf(commandLine, "--data-dir");
        _isolated = dataIndex >= 0;
        var dataDir = dataIndex >= 0 && dataIndex + 1 < commandLine.Length ? commandLine[dataIndex + 1]
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Planer");
        DatabasePath = Path.Combine(Path.GetFullPath(dataDir), "planner.db");
        bool configure = commandLine.Contains("--configure-notifications"), remove = commandLine.Contains("--remove-integration");
        bool background = commandLine.Contains("--notification-check");
        bool startup = commandLine.Contains("--startup");
        if (configure || remove)
        {
            try
            {
                if (_isolated) throw new InvalidOperationException("Системная регистрация недоступна в изолированном запуске");
                var integration = new DesktopIntegration();
                if (remove) { integration.SetNotificationChecks(false); integration.SetAutoStart(false); new WindowsNotifications().Clear(); }
                else integration.SetNotificationChecks(new SqlitePlannerStore(DatabasePath).Load().NotificationsEnabled);
            }
            catch (Exception error) { LogDesktopError(error); Environment.ExitCode = 1; }
            Exit(); return;
        }
        var mutexKey = ActivationBridge.KeyForDatabase(DatabasePath);
        _instanceMutex = new Mutex(true, "Local\\Planer-" + mutexKey, out var firstInstance);
        if (!firstInstance)
        {
            var activation = background || startup ? DesktopActivation.CheckNotifications : destination switch
            { PlannerDestination.Today => DesktopActivation.Today, PlannerDestination.Habits => DesktopActivation.Habits, _ => DesktopActivation.Week };
            if (!await ActivationBridge.Forward(mutexKey, activation)) Environment.ExitCode = 1;
            _instanceMutex.Dispose(); Exit(); return;
        }
        var existed = File.Exists(DatabasePath);
        int captureIndex = Array.IndexOf(commandLine, "--capture");
        if (dataIndex >= 0 && captureIndex >= 0 && captureIndex + 1 < commandLine.Length) CapturePath = Path.GetFullPath(commandLine[captureIndex + 1]);
        CaptureTaskPreview = CapturePath is not null && commandLine.Contains("--capture-task-preview");
        if (dataIndex >= 0 && commandLine.Contains("--trace-drag")) DragTracePath = Path.Combine(Path.GetFullPath(dataDir), "drag-trace.jsonl");
        int scenarioIndex = Array.IndexOf(commandLine, "--scenario");
        if (CapturePath is not null && scenarioIndex >= 0 && scenarioIndex + 1 < commandLine.Length) CaptureScenario = commandLine[scenarioIndex + 1];
        try
        {
            ShellIdentity.Initialize();
            var store = new SqlitePlannerStore(DatabasePath);
            if (!existed && commandLine.Contains("--demo") && dataIndex >= 0) store.Save(DemoData.Create(DateOnly.FromDateTime(DateTime.Now)));
            _engine = new PlannerEngine(store, new SystemPlannerClock());
            if (background)
            {
                if (!_isolated) new WindowsNotifications().Deliver(_engine, DateTime.Now);
                else _engine.CheckNotificationDue(DateTime.Now);
                _instanceMutex.ReleaseMutex(); _instanceMutex.Dispose(); Exit(); return;
            }
            ViewModel = new PlannerViewModel(_engine, _isolated ? new IsolatedDesktopIntegration() : new DesktopIntegration());
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
            _clockTimer.Interval = TimeSpan.FromSeconds(30);
            _clockTimer.Tick += (_, _) => { ViewModel.Run(ViewModel.Tick); CheckNotifications(); }; _clockTimer.Start();
            _engine.Changed += NotificationStateChanged;
            _activationBridge = new ActivationBridge(mutexKey, activation =>
            {
                var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                if (!Window.DispatcherQueue.TryEnqueue(() =>
                {
                    try { HandleActivation(activation); } catch (Exception error) { LogDesktopError(error); }
                    completion.TrySetResult();
                })) completion.TrySetResult();
                return completion.Task;
            });
            Window.Closed += (_, _) => { _clockTimer.Stop(); _engine.Changed -= NotificationStateChanged; _activationBridge.Dispose();
                _tray?.Dispose(); _dayWindow?.Close(); ViewModel.Dispose(); _instanceMutex.ReleaseMutex(); _instanceMutex.Dispose(); };
        }
        catch (Exception ex)
        {
            if (background) { LogDesktopError(ex); Environment.ExitCode = 1; _instanceMutex.Dispose(); Exit(); return; }
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
        if ((CapturePath is not null && CaptureScenario != "StartupHidden") || (!startup && destination != PlannerDestination.Today)) Window.Activate();
        // Ordinary sessions have one icon; bulk rendering does not populate the user's tray.
        if (Window is MainWindow && _clockTimer is not null && (CapturePath is null || CaptureScenario is "TrayLifecycle" or "StartupHidden"))
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
        else if (destination is { } requested) HandleActivation(requested switch
        { PlannerDestination.Today => DesktopActivation.Today, PlannerDestination.Habits => DesktopActivation.Habits, _ => DesktopActivation.Week });
        else if (startup && _tray is null) Window.Activate();
        CheckNotifications();
        if (CaptureScenario == "StartupHidden" && CapturePath is { } startupCapture)
        {
            if (Window.AppWindow.IsVisible || _tray?.IsRegistered != true)
            { File.WriteAllText(startupCapture + ".error.txt", "Автозапуск должен создать значок и оставить главное окно скрытым"); Window.Close(); return; }
            HandleActivation(DesktopActivation.Week);
        }
    }
    internal void HandleActivation(DesktopActivation activation)
    {
        if (activation == DesktopActivation.CheckNotifications) { ViewModel.Run(ViewModel.Tick); CheckNotifications(); return; }
        if (activation == DesktopActivation.Today) { ViewModel.FloatCommand.Execute(null); return; }
        ViewModel.Navigate(activation == DesktopActivation.Habits ? PlannerScreen.Habits : PlannerScreen.Week, ViewModel.Today);
        if (_tray is not null) _tray.Invoke(TrayAction.OpenPlanner);
        else { Window.AppWindow.Show(); Window.Activate(); }
    }
    private void NotificationStateChanged(object? sender, EventArgs args) => Window.DispatcherQueue.TryEnqueue(CheckNotifications);
    private void CheckNotifications()
    {
        if (_isolated || _engine is null || _checkingNotifications) return;
        _checkingNotifications = true;
        try { new WindowsNotifications().Deliver(_engine, DateTime.Now); }
        catch (Exception error) { LogDesktopError(error); }
        finally { _checkingNotifications = false; }
    }
    private static void LogDesktopError(Exception error)
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!); File.AppendAllText(Path.Combine(Path.GetDirectoryName(DatabasePath)!, "desktop.log"), $"{DateTime.Now:O} {error}\n"); }
        catch { }
    }
}
