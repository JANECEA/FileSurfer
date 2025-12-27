using System;
using System.Diagnostics;
using System.IO;
using FileSurfer.Core;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.Shell;

namespace FileSurfer.Linux.Models.Shell;

/// <summary>
/// Linux-specific implementation of <see cref="IShellHandler"/> for shell interactions.
/// Uses <see cref="System.Runtime.InteropServices"/> to interop with the Linux shell.
/// </summary>
public class LinuxShellHandler : IShellHandler
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

    public ValueResult<string> ExecuteShellCommand(string shellCommand, params string[] args) =>
        RunProcess(GetShellPsi(shellCommand, args));

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

    public IResult OpenCmdAt(string dirPath)
    {
        if (string.IsNullOrWhiteSpace(FileSurferSettings.Terminal))
            return SimpleResult.Error("Set terminal in settings.");

        try
        {
            using Process process = new();
            process.StartInfo = GetShellPsi($"{FileSurferSettings.Terminal} \"$1\"", dirPath);
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
            process.StartInfo = GetShellPsi($"{FileSurferSettings.NotepadApp} \"$1\"", filePath);
            process.Start();
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }

    private static ProcessStartInfo GetShellPsi(string shellCommand, params string[] args)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = "/bin/sh",
            ArgumentList = { "-c", shellCommand, "--" },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string arg in args)
            startInfo.ArgumentList.Add(arg);

        return startInfo;
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
