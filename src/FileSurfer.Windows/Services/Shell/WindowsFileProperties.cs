using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using FileSurfer.Core.Models;
using FileSurfer.Core.Services.Shell;
using FileSurfer.Core.ViewModels;

namespace FileSurfer.Windows.Services.Shell;

public class WindowsFileProperties : IFileProperties
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
    [SuppressMessage(
        "Interoperability",
        "SYSLIB1054:Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time",
        Justification = "Wrong suggestion."
    )]
    [SuppressMessage(
        "CodeQuality",
        "IDE0079:Remove unnecessary suppression",
        Justification = "It is necessary."
    )]
    private static extern bool ShellExecuteEx(ref ShellExecuteInfo lpExecInfo);

    public IResult ShowFileProperties(FileSystemEntryViewModel entry)
    {
        ShellExecuteInfo info = new();
        info.cbSize = Marshal.SizeOf(info);
        info.lpVerb = "properties";
        info.lpFile = entry.PathToEntry;
        info.nShow = 0;
        info.fMask = 0x0000000C;

        return ShellExecuteEx(ref info)
            ? SimpleResult.Ok()
            : SimpleResult.Error(new Win32Exception(Marshal.GetLastWin32Error()).Message);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    [SuppressMessage(
        "Interoperability",
        "SYSLIB1054:Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time",
        Justification = "Wrong suggestion."
    )]
    [SuppressMessage(
        "CodeQuality",
        "IDE0079:Remove unnecessary suppression",
        Justification = "It is necessary."
    )]
    private static extern int SHOpenWithDialog(nint hwndParent, ref OpenAsInfo oOAI);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OpenAsInfo
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string cszFile;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? cszClass;

        public OpenAsStyle oaifInFlags;
    }

    [Flags]
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private enum OpenAsStyle
    {
        // Show "always use this app" checkbox
        AllowRegistration = 0x01,

        // Register the extension if user confirms
        RegisterExt = 0x02,

        // Execute the file after the user picks an app
        Exec = 0x04,

        // Always register, even if user cancels
        ForceRegistration = 0x08,

        // Hide the "always use" checkbox
        HideRegistration = 0x20,

        // For URL protocols instead of file extensions
        UrlProtocol = 0x40,

        // Treat cszFile as a URI
        FileIsUri = 0x80,
    }

    public IResult ShowOpenAsDialog(IFileSystemEntry entry)
    {
        OpenAsInfo info = new()
        {
            cszFile = entry.PathToEntry,
            cszClass = null,
            oaifInFlags =
                OpenAsStyle.AllowRegistration | OpenAsStyle.RegisterExt | OpenAsStyle.Exec,
        };

        try
        {
            int hr = SHOpenWithDialog(nint.Zero, ref info);
            return hr == 0
                ? SimpleResult.Ok()
                : SimpleResult.Error(Marshal.GetExceptionForHR(hr)?.Message ?? $"HRESULT {hr}");
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }

    public bool SupportsOpenAs(IFileSystemEntry entry) => entry is FileEntry;
}
