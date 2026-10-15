using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace Planer_App;

internal static class WindowSizing
{
    internal static SizeInt32 Minimum(Window window, bool floating)
    {
        var area = DisplayArea.GetFromWindowId(window.AppWindow.Id, DisplayAreaFallback.Nearest).WorkArea;
        double dpi = GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(window)) / 96d;
        if (dpi <= 0) dpi = 1;
        return new(Math.Min((int)Math.Ceiling((floating ? 300 : 1100) * dpi), Math.Max(1, area.Width - 24)),
            Math.Min((int)Math.Ceiling((floating ? 420 : 720) * dpi), Math.Max(1, area.Height - 24)));
    }
    internal static void Configure(Window window, bool floating)
    {
        void Apply()
        {
            if (window.AppWindow.Presenter is not OverlappedPresenter presenter) return;
            var size = Minimum(window, floating);
            if (presenter.PreferredMinimumWidth != size.Width) presenter.PreferredMinimumWidth = size.Width;
            if (presenter.PreferredMinimumHeight != size.Height) presenter.PreferredMinimumHeight = size.Height;
        }
        Apply();
        window.AppWindow.Changed += (_, args) => { if (args.DidPositionChange) Apply(); };
    }
    internal static void RestoreMinimum(Window window, bool floating)
    {
        var minimum = Minimum(window, floating); var current = window.AppWindow.Size;
        if (current.Width < minimum.Width || current.Height < minimum.Height)
            window.AppWindow.Resize(new(Math.Max(current.Width, minimum.Width), Math.Max(current.Height, minimum.Height)));
    }
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(nint hwnd);
}
