using System;
using System.Diagnostics;
using System.IO;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.Shell;

namespace FileSurfer.Linux.Models.Shell;

/// <summary>
/// Linux-specific implementation of <see cref="IShellHandler"/> for shell interactions.
/// Uses <see cref="System.Runtime.InteropServices"/> to interop with the Linux shell.
/// </summary>
public class LinuxShellHandler : IShellHandler
{
    public IResult OpenCmdAt(string dirPath)
    {
        try
        {
            using Process process = new();
            process.StartInfo = new ProcessStartInfo
            {
                // TODO add preferred terminal setting
                FileName = "wezterm",
                Arguments = "start --cwd .",
                WorkingDirectory = dirPath,
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

    public IResult OpenInNotepad(string filePath, string notepadPath)
    {
        try
        {
            using Process process = new();
            process.StartInfo = new ProcessStartInfo
            {
                // TODO Make separate default Linux settings
                FileName = notepadPath,
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

    public ValueResult<string> ExecuteCommand(string programName, string? args = null)
    {
        using Process process = new();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = programName,
            Arguments = args ?? string.Empty,
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

    public IResult CreateLink(string filePath)
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

            // TODO refine interface to IFileSystemEntry
            _ = File.Exists(filePath)
                ? File.CreateSymbolicLink(linkPath, filePath)
                : Directory.CreateSymbolicLink(linkPath, filePath);

            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }
}
