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

    public IResult OpenInNotepad(string filePath)
    {
        if (string.IsNullOrWhiteSpace(FileSurferSettings.NotepadApp))
            return SimpleResult.Error("Set notepad app in settings.");

        try
        {
            using Process process = new();
            process.StartInfo = GetCmdPsi(
                new ProcessStartInfo(),
                "\"%1\" %2 \"%3\"",
                FileSurferSettings.NotepadApp,
                FileSurferSettings.NotepadAppArgs,
                filePath
            );
            process.Start();
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }

    public IResult OpenCmdAt(string dirPath)
    {
        if (string.IsNullOrWhiteSpace(FileSurferSettings.Terminal))
            return SimpleResult.Error("Set terminal in settings.");

        if (!Directory.Exists(dirPath))
            return SimpleResult.Error("Current directory does not exist.");

        try
        {
            using Process process = new();
            process.StartInfo = GetCmdPsi(
                new ProcessStartInfo(),
                "\"%1\" %2 \"%3\"",
                FileSurferSettings.Terminal,
                FileSurferSettings.TerminalArgs,
                dirPath
            );
            process.Start();
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }

    private static ProcessStartInfo GetCmdPsi(
        ProcessStartInfo? baseInfo,
        string shellCommand,
        params string[] args
    )
    {
        baseInfo ??= new ProcessStartInfo
        {
            FileName = "cmd.exe",
            ArgumentList = { "/c", shellCommand },
            UseShellExecute = true,
        };
        foreach (string arg in args)
            baseInfo.ArgumentList.Add(arg);

        return baseInfo;
    }

    public ValueResult<string> ExecuteCommand(string programName, params string[] args)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = programName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string arg in args)
            startInfo.ArgumentList.Add(arg);

        return RunProcess(startInfo);
    }

    private static ValueResult<string> RunProcess(ProcessStartInfo processStartInfo)
    {
        using Process process = new();
        process.StartInfo = processStartInfo;
        try
        {
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
        catch (Exception ex)
        {
            return ValueResult<string>.Error(ex.Message);
        }
    }
}
