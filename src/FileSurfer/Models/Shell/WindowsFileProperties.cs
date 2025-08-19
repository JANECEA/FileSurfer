using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FileSurfer.Models.Shell;

/// <summary>
/// Provides methods to interact with Windows file properties and dialogs using Windows API calls.
/// </summary>
static class WindowsFileProperties
{
    /// <summary>
    /// Used for the ShellExecuteEx API function.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    struct ShellExecuteInfo
    {
        public int cbSize;
        public uint fMask;
        public nint hwnd;

        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpVerb;

        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpFile;

        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpParameters;

        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpDirectory;
        public int nShow;
        public nint hInstApp;
        public nint lpIDList;

        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpClass;
        public nint hkeyClass;
        public uint dwHotKey;
        public nint hIcon;
        public nint hProcess;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool ShellExecuteEx(ref ShellExecuteInfo lpExecInfo);

    /// <summary>
    /// Calls the <see cref="ShellExecuteEx(ref ShellExecuteInfo)"/> function to show the properties dialog of the specified <paramref name="filePath"/>.
    /// </summary>
    /// <returns><see langword="true"/> if the properties dialog was successfully shown, otherwise <see langword="false"/>.</returns>
    public static IFileOperationResult ShowFileProperties(string filePath)
    {
        ShellExecuteInfo info = new();
        info.cbSize = Marshal.SizeOf(info);
        info.lpVerb = "properties";
        info.lpFile = filePath;
        info.nShow = 0;
        info.fMask = 0x0000000C;

        return ShellExecuteEx(ref info)
            ? FileOperationResult.Ok()
            : FileOperationResult.Error(new Win32Exception(Marshal.GetLastWin32Error()).Message);
    }

    /// <summary>
    /// Displays the "Open With" dialog for a specified file using <c>rundll32.exe</c>.
    /// </summary>
    /// <returns><see langword="true"/> if the "Open With" dialog was successfully shown; otherwise, <see langword="false"/>.</returns>
    public static IFileOperationResult ShowOpenAsDialog(string filePath)
    {
        try
        {
            Process.Start("rundll32.exe", "shell32.dll,OpenAs_RunDLL " + filePath);
            return FileOperationResult.Ok();
        }
        catch (Exception ex)
        {
            return FileOperationResult.Error(ex.Message);
        }
    }
}
