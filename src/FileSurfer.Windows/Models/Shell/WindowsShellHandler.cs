using System;
using System.Diagnostics;
using System.IO;
using FileSurfer.Core;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.Shell;

namespace FileSurfer.Windows.Models.Shell;

/// <summary>
/// Windows-specific implementation of <see cref="IShellHandler"/> for shell interactions.
/// Uses <see cref="System.Runtime.InteropServices"/> to interop with the Windows shell.
/// </summary>
public class WindowsShellHandler : IShellHandler
{
    // TODO fix on Windows
    public IResult OpenCmdAt(string dirPath)
    {
        try
        {
            using Process process = new();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                WorkingDirectory = dirPath,
                Arguments = "-NoExit",
                UseShellExecute = true,
            };
            process.Start();
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }

    public IResult OpenFile(string filePath)
    {
        try
        {
            using Process process = new();
            process.StartInfo = new ProcessStartInfo(filePath) { UseShellExecute = true };
            process.Start();
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }

    // TODO fix on Windows
    public IResult OpenInNotepad(string filePath)
    {
        try
        {
            using Process process = new();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = FileSurferSettings.NotepadApp,
                Arguments = filePath,
                UseShellExecute = true,
            };
            process.Start();
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }

    public ValueResult<string> ExecuteCommand(string programName, string? args = null) =>
        ExecuteShellCommand($"{programName} {args ?? string.Empty}");

    public ValueResult<string> ExecuteShellCommand(string command)
    {
        using Process process = new();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c {command}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        process.Start();
        string stdOut = process.StandardOutput.ReadToEnd();
        string errorMessage = process.StandardError.ReadToEnd();
        process.WaitForExit();

        bool success = process.ExitCode == 0;
        if (!success && string.IsNullOrWhiteSpace(errorMessage))
            errorMessage = stdOut;

        if (success)
            return ValueResult<string>.Ok(stdOut.Trim());

        return string.IsNullOrWhiteSpace(errorMessage)
            ? ValueResult<string>.Error()
            : ValueResult<string>.Error(errorMessage);
    }

    public IResult CreateFileLink(string filePath) => CreateLink(filePath);

    public IResult CreateDirectoryLink(string dirPath) => CreateLink(dirPath);

    private static SimpleResult CreateLink(string path)
    {
        try
        {
            string linkName = Path.GetFileName(path) + " - Shortcut.lnk";
            string parentDir =
                Path.GetDirectoryName(path)
                ?? Path.GetPathRoot(path)
                ?? throw new ArgumentNullException(path);
            string linkPath = Path.Combine(parentDir, linkName);

            IWshRuntimeLibrary.WshShell wshShell = new();
            IWshRuntimeLibrary.IWshShortcut shortcut = wshShell.CreateShortcut(linkPath);

            shortcut.TargetPath = path;
            shortcut.WorkingDirectory = Path.GetDirectoryName(path);
            shortcut.Save();
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }
}
