using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace FileSurfer;

static class WindowsFileProperties
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    struct SHELLEXECUTEINFO
    {
        public int cbSize;
        public uint fMask;
        public IntPtr hwnd;
        [MarshalAs(UnmanagedType.LPTStr)] public string lpVerb;
        [MarshalAs(UnmanagedType.LPTStr)] public string lpFile;
        [MarshalAs(UnmanagedType.LPTStr)] public string lpParameters;
        [MarshalAs(UnmanagedType.LPTStr)] public string lpDirectory;
        public int nShow;
        public IntPtr hInstApp;
        public IntPtr lpIDList;
        [MarshalAs(UnmanagedType.LPTStr)] public string lpClass;
        public IntPtr hkeyClass;
        public uint dwHotKey;
        public IntPtr hIcon;
        public IntPtr hProcess;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO lpExecInfo);

    public static bool ShowFileProperties(string filePath, out string? errorMessage)
    {
        SHELLEXECUTEINFO info = new();
        info.cbSize = Marshal.SizeOf(info);
        info.lpVerb = "properties";
        info.lpFile = filePath;
        info.nShow = 0;
        info.fMask = 0x0000000C;

        bool result = ShellExecuteEx(ref info);
        errorMessage = null;
        if (!result)
        {
            errorMessage = new Win32Exception(Marshal.GetLastWin32Error()).Message;
        }
        return result;
    }
}
