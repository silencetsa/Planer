using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;

namespace Planer_App;

internal static class ShellIdentity
{
    // Keep the installed shortcuts and running windows in the same taskbar group across updates.
    internal const string AppUserModelId = "Planer.Desktop";
    internal static string IconPath
    {
        get
        {
            // A new resource path prevents Explorer from reusing artwork cached for an older release.
            var version = typeof(App).Assembly.GetName().Version!.ToString(3);
            var installed = Path.Combine(AppContext.BaseDirectory, "Assets", $"AppIcon-{version}.ico");
            return File.Exists(installed) ? installed : Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        }
    }

    internal static void Initialize() => Marshal.ThrowExceptionForHR(SetCurrentProcessExplicitAppUserModelID(AppUserModelId));

    internal static void VerifyWindowIcon(Window window)
    {
        Marshal.ThrowExceptionForHR(GetCurrentProcessExplicitAppUserModelID(out var identity));
        try
        {
            if (Marshal.PtrToStringUni(identity) != AppUserModelId)
                throw new InvalidOperationException("Идентификатор приложения не совпадает с ярлыками установщика");
        }
        finally { Marshal.FreeCoTaskMem(identity); }
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        if (SendMessage(hwnd, 0x007F, 0, 0) == 0 || SendMessage(hwnd, 0x007F, 1, 0) == 0)
            throw new InvalidOperationException("У окна не заданы маленькая и большая иконки панели задач");
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
    [DllImport("shell32.dll")]
    private static extern int GetCurrentProcessExplicitAppUserModelID(out nint appId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint SendMessage(nint hwnd, uint message, nint wParam, nint lParam);
}
