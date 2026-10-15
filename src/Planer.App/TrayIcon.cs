using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.UI.Xaml;
using Planer_App.ViewModels;

namespace Planer_App;

internal enum TrayAction { OpenPlanner = 1, OpenToday = 2, Exit = 3 }

/// <summary>Native notification-area icon; it does not change the main window's close behavior.</summary>
internal sealed class TrayIcon : IDisposable
{
    private const uint CallbackMessage = 0x8031;
    private readonly Window _window;
    private readonly PlannerViewModel _vm;
    private readonly nint _hwnd;
    private readonly Native.SubclassProc _callback;
    private readonly uint _taskbarCreated;
    private readonly Action<Exception> _report;
    private Native.NotifyIconData _data;
    private bool _registered, _disposed;

    public TrayIcon(Window window, PlannerViewModel vm, Action<Exception> report)
    {
        _window = window; _vm = vm; _report = report; _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        _callback = WindowProcedure; _taskbarCreated = Native.RegisterWindowMessage("TaskbarCreated");
        int size = Native.GetSystemMetricsForDpi(49, Native.GetDpiForWindow(_hwnd));
        nint icon = Native.LoadImage(0, Path.Combine(AppContext.BaseDirectory, "Assets", "TrayIcon.ico"), 1, size, size, 0x10);
        if (icon == 0) throw new Win32Exception(Marshal.GetLastWin32Error(), "Не удалось загрузить иконку области уведомлений");
        var identity = AppContext.BaseDirectory + "|" + App.DatabasePath;
        _data = new Native.NotifyIconData
        {
            Size = (uint)Marshal.SizeOf<Native.NotifyIconData>(), Window = _hwnd, Id = 1,
            Flags = 1 | 2 | 4 | 0x20, Callback = CallbackMessage, Icon = icon,
            Tip = "Планер", Info = "", InfoTitle = "", Guid = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(identity.ToUpperInvariant()))[..16])
        };
        if (!Native.SetWindowSubclass(_hwnd, _callback, CallbackMessage, 0))
        { Native.DestroyIcon(icon); throw new Win32Exception(Marshal.GetLastWin32Error(), "Не удалось подключить меню области уведомлений"); }
        try { Register(); }
        catch { Dispose(); throw; }
    }

    public bool IsRegistered
    {
        get
        {
            var id = new Native.IconIdentifier { Size = (uint)Marshal.SizeOf<Native.IconIdentifier>(), Window = _hwnd, Id = 1, Guid = _data.Guid };
            return Native.Shell_NotifyIconGetRect(ref id, out var rectangle) == 0 && rectangle.Right > rectangle.Left;
        }
    }
    private void Register()
    {
        _registered = Native.Shell_NotifyIcon(0, ref _data);
        if (!_registered) throw new InvalidOperationException("Windows не добавила значок планера в область уведомлений");
        _data.Version = 4;
        if (!Native.Shell_NotifyIcon(4, ref _data)) throw new InvalidOperationException("Windows не включила обработку меню значка планера");
    }
    private nint WindowProcedure(nint hwnd, uint message, nuint wParam, nint lParam, nuint subclass, nuint reference)
    {
        try
        {
            if (message == _taskbarCreated && _taskbarCreated != 0) { _registered = false; Register(); }
            if (message == CallbackMessage)
            {
                uint action = (uint)(lParam.ToInt64() & 0xffff);
                if (action == 0x7b) ShowMenu();
                else if (action is 0x400 or 0x401) _window.DispatcherQueue.TryEnqueue(() => Invoke(TrayAction.OpenPlanner));
                return 0;
            }
        }
        catch (Exception ex) { _report(ex); }
        return Native.DefSubclassProc(hwnd, message, wParam, lParam);
    }
    private void ShowMenu()
    {
        nint menu = Native.CreatePopupMenu();
        if (menu == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        uint selected;
        try
        {
            Native.AppendMenu(menu, 0, (nuint)TrayAction.OpenPlanner, "Открыть планер");
            Native.AppendMenu(menu, 0, (nuint)TrayAction.OpenToday, "Сегодня поверх окон");
            Native.AppendMenu(menu, 0x800, 0, null);
            Native.AppendMenu(menu, 0, (nuint)TrayAction.Exit, "Выход");
            Native.GetCursorPos(out var point);
            Native.SetForegroundWindow(_hwnd);
            selected = Native.TrackPopupMenuEx(menu, 0x100 | 0x80 | 2, point.X, point.Y, _hwnd, 0);
            Native.PostMessage(_hwnd, 0, 0, 0);
        }
        finally { Native.DestroyMenu(menu); }
        if (selected is >= 1 and <= 3) _window.DispatcherQueue.TryEnqueue(() => Invoke((TrayAction)selected));
        else Native.Shell_NotifyIcon(3, ref _data);
    }
    internal void Invoke(TrayAction action)
    {
        switch (action)
        {
            case TrayAction.OpenPlanner:
                if (Native.IsIconic(_hwnd)) Native.ShowWindow(_hwnd, 9);
                _window.AppWindow.Show(); _window.Activate(); Native.SetForegroundWindow(_hwnd); break;
            case TrayAction.OpenToday: _vm.FloatCommand.Execute(null); break;
            case TrayAction.Exit: _window.Close(); break;
        }
    }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        if (_registered) Native.Shell_NotifyIcon(2, ref _data);
        _registered = false; Native.RemoveWindowSubclass(_hwnd, _callback, CallbackMessage);
        if (_data.Icon != 0) { Native.DestroyIcon(_data.Icon); _data.Icon = 0; }
    }

    private static class Native
    {
        internal delegate nint SubclassProc(nint hwnd, uint message, nuint wParam, nint lParam, nuint subclass, nuint reference);
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct NotifyIconData
        {
            public uint Size; public nint Window; public uint Id, Flags, Callback; public nint Icon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
            public uint State, StateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
            public uint Version;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string InfoTitle;
            public uint InfoFlags; public Guid Guid; public nint BalloonIcon;
        }
        [StructLayout(LayoutKind.Sequential)] internal struct IconIdentifier { public uint Size; public nint Window; public uint Id; public Guid Guid; }
        [StructLayout(LayoutKind.Sequential)] internal struct Rectangle { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; }
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "Shell_NotifyIconW")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool Shell_NotifyIcon(uint message, ref NotifyIconData data);
        [DllImport("shell32.dll")] internal static extern int Shell_NotifyIconGetRect(ref IconIdentifier id, out Rectangle rectangle);
        [DllImport("comctl32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool SetWindowSubclass(nint window, SubclassProc proc, nuint id, nuint reference);
        [DllImport("comctl32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool RemoveWindowSubclass(nint window, SubclassProc proc, nuint id);
        [DllImport("comctl32.dll")] internal static extern nint DefSubclassProc(nint window, uint message, nuint wParam, nint lParam);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegisterWindowMessageW")] internal static extern uint RegisterWindowMessage(string name);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "LoadImageW")] internal static extern nint LoadImage(nint instance, string name, uint type, int width, int height, uint flags);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool DestroyIcon(nint icon);
        [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(nint window);
        [DllImport("user32.dll")] internal static extern int GetSystemMetricsForDpi(int index, uint dpi);
        [DllImport("user32.dll", SetLastError = true)] internal static extern nint CreatePopupMenu();
        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "AppendMenuW")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool AppendMenu(nint menu, uint flags, nuint id, string? text);
        [DllImport("user32.dll")] internal static extern uint TrackPopupMenuEx(nint menu, uint flags, int x, int y, nint window, nint parameters);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool DestroyMenu(nint menu);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GetCursorPos(out Point point);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool SetForegroundWindow(nint window);
        [DllImport("user32.dll", EntryPoint = "PostMessageW")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool PostMessage(nint window, uint message, nuint wParam, nint lParam);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool IsIconic(nint window);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool ShowWindow(nint window, int command);
    }
}
