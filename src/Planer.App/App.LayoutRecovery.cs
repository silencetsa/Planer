using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace Planer_App;

public partial class App
{
    private bool _layoutRecoveryPending;
    private int _consecutiveLayoutFailures;
    private DateTime _lastLayoutFailure;
    internal int LayoutCycleCount { get; private set; }
    internal int LayoutRecoveryCount { get; private set; }
    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs args)
    {
        LogException(args.Exception, args.Message);
        // Only layout cycles are recoverable here. Other failures keep their normal behavior.
        if (!RecoverLayoutCycle(args.Exception, args.Message)) return;
        args.Handled = true;
        // Recovery must not turn an unexpected QA layout error into a passing screenshot.
        if (CapturePath is { } path)
            try { File.WriteAllText(path + ".error.txt", args.Exception.ToString()); } catch { }
    }
    private static bool IsLayoutCycle(Exception exception, string? nativeMessage) => exception is LayoutCycleException
        // UnhandledException.Exception may carry stale WinRT error details. The native Message
        // identifies the original XAML failure even when its projection is a COMException.
        || nativeMessage?.Contains("Layout cycle detected", StringComparison.OrdinalIgnoreCase) == true;
    internal bool RecoverLayoutCycle(Exception exception, string? nativeMessage = null)
    {
        if (!IsLayoutCycle(exception, nativeMessage) || Window is not MainWindow) return false;
        LayoutCycleCount++;
        if (DateTime.UtcNow - _lastLayoutFailure > TimeSpan.FromSeconds(30)) _consecutiveLayoutFailures = 0;
        _lastLayoutFailure = DateTime.UtcNow;
        _consecutiveLayoutFailures++;
        QueueLayoutRecovery(); return true;
    }
    private void QueueLayoutRecovery()
    {
        if (_layoutRecoveryPending) return;
        _layoutRecoveryPending = true;
        // Rebuild after the failing native layout pass has unwound, never recursively inside it.
        if (!Window.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.High, () =>
        {
            try
            {
                Views.Ui.CloseDialogsForLayoutRecovery();
                bool fallback = _consecutiveLayoutFailures > 2;
                ((MainWindow)Window).RecoverLayout(fallback); _dayWindow?.RecoverLayout(fallback);
                LayoutRecoveryCount++;
            }
            finally { _layoutRecoveryPending = false; }
        })) _layoutRecoveryPending = false;
    }
    internal void RetryLayout()
    {
        _consecutiveLayoutFailures = 0; QueueLayoutRecovery();
    }
    private static void LogException(Exception exception, string? nativeMessage)
    {
        try
        {
            if (Path.GetDirectoryName(DatabasePath) is { Length: > 0 } directory)
                File.AppendAllText(Path.Combine(directory, "crash.log"), $"{DateTime.Now:O} XAML: {nativeMessage}\n{exception}\n");
        }
        catch { }
    }
}
