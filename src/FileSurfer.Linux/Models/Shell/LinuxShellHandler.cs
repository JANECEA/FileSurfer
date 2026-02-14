using System;
using System.Diagnostics;
using System.IO;
using FileSurfer.Core;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.Shell;

namespace FileSurfer.Linux.Models.Shell;

public interface IShellCommandHandler : ILocalShellHandler
{
    /// <summary>
    /// Executes a shell command in the command prompt.
    /// </summary>
    /// <param name="shellCommand">Shell command to execute</param>
    /// <param name="args">Arguments for the shell command's $variables</param>
    /// <returns>A <see cref="ValueResult{string}"/> representing the result stdout of the operation and potential errors.</returns>
    public ValueResult<string> ExecuteShellCommand(string shellCommand, params string[] args);
}

/// <summary>
/// Linux-specific implementation of <see cref="IShellHandler"/> for shell interactions.
/// Uses <see cref="System.Runtime.InteropServices"/> to interop with the Linux shell.
/// </summary>
public class LinuxShellHandler : IShellCommandHandler
{
    public IResult CreateFileLink(string filePath)
    {
        try
        {
            string extension = Path.GetExtension(filePath);
            string linkName = $"{Path.GetFileName(filePath)} - link{extension}";
            string parentDir =
                Path.GetDirectoryName(filePath)
                ?? Path.GetPathRoot(filePath)
                ?? throw new ArgumentNullException(filePath);

            string linkPath = Path.Combine(parentDir, linkName);
            File.CreateSymbolicLink(linkPath, filePath);
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }

    public IResult CreateDirectoryLink(string dirPath)
    {
        try
        {
            string linkName = $"{Path.GetFileName(dirPath)} - link";
            string parentDir =
                Path.GetDirectoryName(dirPath)
                ?? Path.GetPathRoot(dirPath)
                ?? throw new ArgumentNullException(dirPath);

            string linkPath = Path.Combine(parentDir, linkName);
            Directory.CreateSymbolicLink(linkPath, dirPath);
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
            process.StartInfo = GetShellPsi(
                new ProcessStartInfo(),
                "\"$1\" $2 \"$3\"",
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
            process.StartInfo = GetShellPsi(
                new ProcessStartInfo(),
                "\"$1\" $2 \"$3\"",
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

    public ValueResult<string> ExecuteCommand(string programName, params string[] args)
    {
        ProcessStartInfo psi = new()
        {
            FileName = programName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string arg in args)
            psi.ArgumentList.Add(arg);

        return RunProcess(psi);
    }

    public ValueResult<string> ExecuteShellCommand(string shellCommand, params string[] args) =>
        RunProcess(GetShellPsi(null, shellCommand, args));

    private static ProcessStartInfo GetShellPsi(
        ProcessStartInfo? baseInfo,
        string shellCommand,
        params string[] args
    )
    {
        baseInfo ??= new ProcessStartInfo
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        baseInfo.FileName = "/bin/sh";
        baseInfo.ArgumentList.Add("-c");
        baseInfo.ArgumentList.Add(shellCommand);
        baseInfo.ArgumentList.Add("--");

        foreach (string arg in args)
            baseInfo.ArgumentList.Add(arg);

        return baseInfo;
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
            if (success)
                return stdOut.Trim().OkResult();

            if (string.IsNullOrWhiteSpace(errorMessage))
                errorMessage = stdOut;

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
