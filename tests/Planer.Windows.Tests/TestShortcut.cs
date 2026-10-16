using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

internal sealed class TestShortcut : IDisposable
{
    private readonly string _path;
    public TestShortcut(string appId)
    {
        if (!appId.StartsWith("Planer.Desktop.QA.", StringComparison.Ordinal)) throw new InvalidOperationException("Expected isolated app ID");
        _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), appId + ".lnk");
        object link = new ShellLink();
        try
        {
            ((IShellLink)link).SetPath(Environment.ProcessPath!);
            Set((IPropertyStore)link, 5, 31, Marshal.StringToCoTaskMemUni(appId));
            var guid = Marshal.AllocCoTaskMem(16); Marshal.StructureToPtr(new Guid("46D3566D-8F52-45F8-9F20-891FC403878D"), guid, false);
            Set((IPropertyStore)link, 26, 72, guid);
            ((IPropertyStore)link).Commit(); ((IPersistFile)link).Save(_path, true);
            Marshal.ThrowExceptionForHR(SetCurrentProcessExplicitAppUserModelID(appId));
            SHChangeNotify(2, 0x1005, _path, 0);
        }
        finally { Marshal.FinalReleaseComObject(link); }
    }
    private static void Set(IPropertyStore store, uint id, ushort type, nint pointer)
    {
        var key = new PropertyKey { Format = new("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), Id = id };
        var value = new PropertyValue { Type = type, Pointer = pointer };
        try { store.SetValue(ref key, ref value); }
        finally { Marshal.FreeCoTaskMem(pointer); }
    }
    public void Dispose() => File.Delete(_path);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern int SetCurrentProcessExplicitAppUserModelID(string id);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern void SHChangeNotify(int change, uint flags, string path, nint other);
    [StructLayout(LayoutKind.Sequential)] private struct PropertyKey { public Guid Format; public uint Id; }
    [StructLayout(LayoutKind.Explicit, Size = 24)] private struct PropertyValue { [FieldOffset(0)] public ushort Type; [FieldOffset(8)] public nint Pointer; }
    [ComImport, Guid("00021401-0000-0000-C000-000000000046")] private class ShellLink { }
    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        void GetCount(out uint count); void GetAt(uint index, out PropertyKey key); void GetValue(ref PropertyKey key, out PropertyValue value);
        void SetValue(ref PropertyKey key, ref PropertyValue value); void Commit();
    }
    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLink
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int count, nint findData, uint flags);
        void GetIDList(out nint list); void SetIDList(nint list);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder description, int count);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string description);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder directory, int count);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder arguments, int count);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
        void GetHotkey(out short key); void SetHotkey(short key); void GetShowCmd(out int command); void SetShowCmd(int command);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder icon, int count, out int index);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string icon, int index);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved); void Resolve(nint window, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
    }
}
