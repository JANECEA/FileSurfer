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

    public ValueResult<string> ExecuteShellCommand(string command)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = "/bin/sh",
            Arguments = $"-c \"{command.Replace("\"", "\\\"")}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        return ManageProcess(startInfo);
    }

    public ValueResult<string> ExecuteCommand(string programName, string? args = null)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = programName,
            Arguments = args ?? string.Empty,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        return ManageProcess(startInfo);
    }

    private static ValueResult<string> ManageProcess(ProcessStartInfo processStartInfo)
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
}
