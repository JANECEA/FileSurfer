using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.Shell;

namespace FileSurfer.Linux.Models.Shell;

public class LinuxFileProperties : IFileProperties
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct ShellExecuteInfo
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
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Interoperability",
        "SYSLIB1054:Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time",
        Justification = "Wrong suggestion."
    )]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "CodeQuality",
        "IDE0079:Remove unnecessary suppression",
        Justification = "It is necessary."
    )]
    private static extern bool ShellExecuteEx(ref ShellExecuteInfo lpExecInfo);

    public IResult ShowFileProperties(string filePath)
    {
        ShellExecuteInfo info = new();
        info.cbSize = Marshal.SizeOf(info);
        info.lpVerb = "properties";
        info.lpFile = filePath;
        info.nShow = 0;
        info.fMask = 0x0000000C;

        return ShellExecuteEx(ref info)
            ? SimpleResult.Ok()
            : SimpleResult.Error(
                new Win32Exception(Marshal.GetLastWin32Error()).Message
            );
    }

    public IResult ShowOpenAsDialog(string filePath)
    {
        try
        {
            Process.Start(
                "rundll32.exe",
                "shell32.dll,OpenAs_RunDLL " + filePath
            );
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }
}
