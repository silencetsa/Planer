using System.Runtime.InteropServices;
using Planer.Core;

namespace Planer_App;

/// <summary>Native notification activation, including when Windows starts a closed planner.</summary>
internal sealed class ToastActivationBridge : IDisposable
{
    internal const string ClassIdText = "46D3566D-8F52-45F8-9F20-891FC403878D";
    private readonly ToastClassFactory _factory;
    private uint _cookie;
    internal ToastActivationBridge(Func<PlannerDestination, bool> activate, Guid? classId = null, string? appId = null)
    {
        _factory = new(new ToastActivationCallback(appId ?? ShellIdentity.AppUserModelId, activate));
        var id = classId ?? new Guid(ClassIdText);
        Marshal.ThrowExceptionForHR(CoRegisterClassObject(ref id, _factory, 4 /* local server */, 1 /* multiple use */, out _cookie));
    }
    public void Dispose()
    {
        if (_cookie == 0) return;
        CoRevokeClassObject(_cookie); _cookie = 0;
        GC.KeepAlive(_factory);
    }
    [DllImport("ole32.dll")] private static extern int CoRegisterClassObject(ref Guid id, [MarshalAs(UnmanagedType.IUnknown)] object factory, uint context, uint flags, out uint cookie);
    [DllImport("ole32.dll")] private static extern int CoRevokeClassObject(uint cookie);
}

[ComVisible(true), Guid("53E31837-6600-4A81-9395-75CFFE746F94"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IToastActivationCallback
{
    [PreserveSig] int Activate([MarshalAs(UnmanagedType.LPWStr)] string appId, [MarshalAs(UnmanagedType.LPWStr)] string arguments, nint input, uint count);
}

[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
public sealed class ToastActivationCallback : IToastActivationCallback
{
    private readonly string _appId;
    private readonly Func<PlannerDestination, bool> _activate;
    internal ToastActivationCallback(string appId, Func<PlannerDestination, bool> activate) { _appId = appId; _activate = activate; }
    public int Activate(string appId, string arguments, nint input, uint count)
    {
        // Notifications contain only one fixed destination, never paths, command lines or user input.
        if (appId != _appId) return unchecked((int)0x80070005);
        if (WindowsNotifications.ParseActivation(arguments) is not { } destination || count != 0) return unchecked((int)0x80070057);
        try { return _activate(destination) ? 0 : unchecked((int)0x80004005); }
        catch (Exception error) { return Marshal.GetHRForException(error); }
    }
}

[ComVisible(true), Guid("00000001-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IToastClassFactory
{
    [PreserveSig] int CreateInstance(nint outer, ref Guid interfaceId, out nint instance);
    [PreserveSig] int LockServer([MarshalAs(UnmanagedType.Bool)] bool locked);
}

[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
public sealed class ToastClassFactory : IToastClassFactory
{
    private readonly ToastActivationCallback _callback;
    internal ToastClassFactory(ToastActivationCallback callback) => _callback = callback;
    public int CreateInstance(nint outer, ref Guid interfaceId, out nint instance)
    {
        instance = 0;
        if (outer != 0) return unchecked((int)0x80040110);
        nint unknown = 0;
        try { unknown = Marshal.GetIUnknownForObject(_callback); return Marshal.QueryInterface(unknown, in interfaceId, out instance); }
        catch (Exception error) { return Marshal.GetHRForException(error); }
        finally { if (unknown != 0) Marshal.Release(unknown); }
    }
    public int LockServer(bool locked) => 0;
}
