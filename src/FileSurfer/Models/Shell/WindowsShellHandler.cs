using System;
using System.Diagnostics;
using System.IO;

namespace FileSurfer.Models.Shell;

/// <summary>
/// Provides methods to interact with Windows file properties and dialogs using Windows API calls.
/// </summary>
public class WindowsShellHandler : IShellHandler
{
    public bool OpenCmdAt(string dirPath, out string? errorMessage)
    {
        try
        {
            new Process
            {
                StartInfo = new()
                {
                    FileName = "powershell.exe",
                    WorkingDirectory = dirPath,
                    Arguments = "-NoExit",
                    UseShellExecute = true,
                },
            }.Start();
            errorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public bool OpenFile(string filePath, out string? errorMessage)
    {
        try
        {
            new Process
            {
                StartInfo = new ProcessStartInfo(filePath) { UseShellExecute = true },
            }.Start();
            errorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public bool OpenInNotepad(string filePath, out string? errorMessage)
    {
        try
        {
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = FileSurferSettings.NotepadApp,
                    Arguments = filePath,
                    UseShellExecute = true,
                }
            );
            errorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public bool ExecuteCmd(string command, out string? errorMessage)
    {
        using Process process = new();
        process.StartInfo = new()
        {
            FileName = "cmd.exe",
            Arguments = "/c " + command,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        process.Start();
        string stdOut = process.StandardOutput.ReadToEnd();
        errorMessage = process.StandardError.ReadToEnd();
        process.WaitForExit();
        bool success = process.ExitCode == 0;

        if (!success && errorMessage == string.Empty)
            errorMessage = stdOut;

        if (errorMessage == string.Empty)
            errorMessage = null;

        return success;
    }

    public bool CreateLink(string filePath, out string? errorMessage)
    {
        try
        {
            string linkName = Path.GetFileName(filePath) + " - Shortcut.lnk";
            string parentDir =
                Path.GetDirectoryName(filePath)
                ?? Path.GetPathRoot(filePath)
                ?? throw new ArgumentNullException(filePath);
            string linkPath = Path.Combine(parentDir, linkName);

            IWshRuntimeLibrary.WshShell wshShell = new();
            IWshRuntimeLibrary.IWshShortcut shortcut = wshShell.CreateShortcut(linkPath);

            shortcut.TargetPath = filePath;
            shortcut.WorkingDirectory = Path.GetDirectoryName(filePath);
            shortcut.Save();
            errorMessage = null;
            return true;
        }
        catch (Exception e)
        {
            errorMessage = e.Message;
            return false;
        }
    }
}
