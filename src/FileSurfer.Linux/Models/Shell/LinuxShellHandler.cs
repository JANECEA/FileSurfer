using System;
using System.Diagnostics;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.Shell;

namespace FileSurfer.Linux.Models.Shell;

/// <summary>
/// Windows-specific implementation of <see cref="IShellHandler"/> for shell interactions.
/// Uses <see cref="System.Runtime.InteropServices"/> to interop with the Windows shell.
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
                FileName = "bash",
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

    public IResult OpenInNotepad(string filePath, string notepadPath)
    {
        try
        {
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = notepadPath,
                    Arguments = filePath,
                    UseShellExecute = true,
                }
            );
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }

    public IResult ExecuteCmd(string command)
    {
        using Process process = new();
        process.StartInfo = new ProcessStartInfo
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
        string? errorMessage = process.StandardError.ReadToEnd();
        process.WaitForExit();

        bool success = process.ExitCode == 0;
        if (!success && string.IsNullOrWhiteSpace(errorMessage))
            errorMessage = stdOut;

        if (string.IsNullOrWhiteSpace(errorMessage))
            errorMessage = null;

        if (success)
            return SimpleResult.Ok();

        return errorMessage is null ? SimpleResult.Error() : SimpleResult.Error(errorMessage);
    }

    public IResult CreateLink(string filePath)
    {
        return SimpleResult.Error("Not implemented");
    }
}
