using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using FileSurfer.Core;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.Shell;

namespace FileSurfer.Windows.Models.Shell;

/// <summary>
/// Windows-specific implementation of <see cref="IShellHandler"/> for shell interactions.
/// Uses <see cref="System.Runtime.InteropServices"/> to interop with the Windows shell.
/// </summary>
#pragma warning disable SYSLIB1054
public class WindowsShellHandler : ILocalShellHandler
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

        ValueResult<ProcessStartInfo> psiResult = GetCmdPsi(
            FileSurferSettings.NotepadApp,
            FileSurferSettings.NotepadAppArgs,
            filePath
        );
        if (!psiResult.IsOk)
            return psiResult;

        try
        {
            using Process process = new();
            process.StartInfo = psiResult.Value;
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

        ValueResult<ProcessStartInfo> psiResult = GetCmdPsi(
            FileSurferSettings.Terminal,
            FileSurferSettings.TerminalArgs,
            "."
        );
        if (!psiResult.IsOk)
            return psiResult;

        psiResult.Value.WorkingDirectory = dirPath;
        try
        {
            using Process process = new();
            process.StartInfo = psiResult.Value;
            process.Start();
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }

    private static ValueResult<ProcessStartInfo> GetCmdPsi(
        string executablePath,
        string args,
        string filePath
    )
    {
        List<string> splitArgs = new();
        if (!string.IsNullOrWhiteSpace(args))
        {
            ValueResult<List<string>> result = SplitWindowsCommandLine(args);
            if (!result.IsOk)
                return ValueResult<ProcessStartInfo>.Error(result.Errors.First());

            splitArgs = result.Value;
        }
        ProcessStartInfo baseInfo = new() { FileName = executablePath, UseShellExecute = true };

        foreach (string arg in splitArgs)
            baseInfo.ArgumentList.Add(arg);

        baseInfo.ArgumentList.Add(filePath);
        return baseInfo.OkResult();
    }

    private static ValueResult<List<string>> SplitWindowsCommandLine(string commandLine)
    {
        IntPtr argv = CommandLineToArgvW(commandLine, out int argc);
        if (argv == IntPtr.Zero)
            return ValueResult<List<string>>.Error(
                $"Failed to parse arguments: \"{commandLine}\"."
            );

        try
        {
            List<string> args = new();
            for (int i = 0; i < argc; i++)
            {
                IntPtr p = Marshal.ReadIntPtr(argv, i * IntPtr.Size);
                args.Add(Marshal.PtrToStringUni(p)!);
            }
            return args.OkResult();
        }
        finally
        {
            Marshal.FreeHGlobal(argv);
        }
    }

    [DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CommandLineToArgvW(string lpCmdLine, out int pNumArgs);

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
                return stdOut.Trim().OkResult();

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
#pragma warning restore SYSLIB1054
