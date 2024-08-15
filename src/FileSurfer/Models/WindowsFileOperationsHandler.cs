using Microsoft.VisualBasic.FileIO;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;

namespace FileSurfer;

class WindowsFileOperationsHandler : IFileOperationsHandler
{
    public bool DeleteFile(string filePath, out string? errorMessage)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                errorMessage = $"Could not find file: \"{filePath}\"";
                return false;
            }
            File.Delete(filePath);
            errorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public bool DeleteDir(string dirPath, out string? errorMessage)
    {
        try
        {
            Directory.Delete(dirPath);
            errorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public string[] GetDrives(out string? errorMessage)
    {
        try
        {
            errorMessage = null;
            return Directory.GetLogicalDrives();
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return Array.Empty<string>();
        }
    }

    public string[] GetPathFiles(string path, bool includeHidden, bool includeProtectedByOS)
    {
        try
        {
            string[] files = Directory.GetFiles(path);

            if (!includeHidden)
            {
                for (int i = 0; i < files.Length; i++)
                {
                    if (IsHidden(files[i], false))
                        files[i] = string.Empty;
                }
            }
            if (!includeProtectedByOS)
            {
                for (int i = 0; i < files.Length; i++)
                {
                    if (IsProtected(files[i], false))
                        files[i] = string.Empty;
                }
            }

            if (!includeHidden || !includeProtectedByOS)
                return files.Where(filePath => filePath != string.Empty).ToArray();
            return files;
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public string[] GetPathDirs(string path, bool includeHidden, bool includeProtectedByOS)
    {
        try
        {
            string[] directories = Directory.GetDirectories(path);

            if (!includeHidden)
            {
                for (int i = 0; i < directories.Length; i++)
                {
                    if (IsHidden(directories[i], true))
                        directories[i] = string.Empty;
                }
            }
            if (!includeProtectedByOS)
            {
                for (int i = 0; i < directories.Length; i++)
                {
                    if (IsProtected(directories[i], true))
                        directories[i] = string.Empty;
                }
            }

            if (!includeHidden || !includeProtectedByOS)
                return directories.Where(dirPath => dirPath != string.Empty).ToArray();
            return directories;
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public Icon? GetFileIcon(string path)
    {
        try
        {
            return Icon.ExtractAssociatedIcon(path);
        }
        catch
        {
            return null;
        }
    }

    public long GetFileSizeB(string path)
    {
        try
        {
            return new FileInfo(path).Length;
        }
        catch
        {
            return 0;
        }
    }

    public DateTime? GetFileLastModified(string filePath)
    {
        try
        {
            return new FileInfo(filePath).LastWriteTime;
        }
        catch
        {
            return null;
        }
    }

    public DateTime? GetDirLastModified(string dirPath)
    {
        try
        {
            return new DirectoryInfo(dirPath).LastWriteTime;
        }
        catch
        {
            return null;
        }
    }

    public bool MoveFileToTrash(string filePath, out string? errorMessage)
    {
        try
        {
            FileSystem.DeleteFile(
                filePath,
                UIOption.OnlyErrorDialogs,
                RecycleOption.SendToRecycleBin
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

    public bool MoveDirToTrash(string dirPath, out string? errorMessage)
    {
        try
        {
            FileSystem.DeleteDirectory(
                dirPath,
                UIOption.OnlyErrorDialogs,
                RecycleOption.SendToRecycleBin
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

    public bool RestoreFile(string ogFilePath, out string? errorMessage) =>
        WindowsFileRestorer.RestoreFile(ogFilePath, out errorMessage);

    public bool RestoreDir(string ogDirPath, out string? errorMessage) =>
        WindowsFileRestorer.RestoreDir(ogDirPath, out errorMessage);

    public bool NewFileAt(string dirPath, string fileName, out string? errorMessage)
    {
        try
        {
            string filePath = Path.Combine(dirPath, fileName);
            if (File.Exists(filePath))
            {
                errorMessage = $"File: \"{filePath}\" already exists";
                return false;
            }
            using FileStream file = File.Create(filePath);
            file.Close();
            errorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public bool NewDirAt(string dirPath, string dirName, out string? errorMessage)
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(dirPath, dirName));
            errorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public bool IsHidden(string path, bool isDirectory)
    {
        try
        {
            return
                isDirectory
                ? new DirectoryInfo(path).Attributes.HasFlag(FileAttributes.Hidden)
                : new FileInfo(path).Attributes.HasFlag(FileAttributes.Hidden);
        }
        catch
        {
            return false;
        }
    }

    public static bool IsProtected(string path, bool isDirectory)
    {
        try
        {
            return
                isDirectory
                ? new DirectoryInfo(path).Attributes.HasFlag(FileAttributes.System)
                : new FileInfo(path).Attributes.HasFlag(FileAttributes.System);
        }
        catch
        {
            return true;
        }
    }

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
                    UseShellExecute = true
                }
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
                StartInfo = new ProcessStartInfo(filePath) { UseShellExecute = true }
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

    public bool CopyFileToSystemClipBoard(string filePath, out string? errorMessage)
    {
        string command = $"Set-Clipboard -Path {filePath}";
        return PowerShellCommand(command, out errorMessage);
    }

    public bool PasteFileFromClipBoardAt(string filePath, out string? errorMessage)
    {
        string command =
            "foreach ($file in Get-Clipboard -Format FileDropList) { $destFile = Join-Path"
            + filePath
            + "(Split-Path $file -Leaf) Copy-Item -Path $file -Destination $destFile }";
        return PowerShellCommand(command, out errorMessage);
    }

    private static bool PowerShellCommand(string command, out string? errorMessage)
    {
        using Process process =
            new()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = "powershell.exe",
                    Arguments = "/c " + command,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        if (process.ExitCode == 0)
        {
            errorMessage = null;
            return true;
        }
        errorMessage = process.StandardError.ReadToEnd();
        return false;
    }

    public bool ExecuteCmd(string command, out string? errorMessage)
    {
        using Process process =
            new()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = "cmd.exe",
                    Arguments = "/c " + command,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        if (process.ExitCode == 0)
        {
            errorMessage = null;
            return true;
        }
        errorMessage = process.StandardError.ReadToEnd();
        return false;
    }

    public bool IsValidFileName(string fileName)
    {
        char[] invalidChars = Path.GetInvalidFileNameChars();
        foreach (char c in fileName)
        {
            if (invalidChars.Contains(c))
                return false;
        }
        return true;
    }

    public bool IsValidDirName(string dirName)
    {
        char[] invalidChars = Path.GetInvalidPathChars();
        foreach (char c in dirName)
        {
            if (invalidChars.Contains(c))
                return false;
        }
        return true;
    }

    public bool RenameFileAt(string filePath, string newName, out string? errorMessage)
    {
        try
        {
            string? pathToFile = Path.GetDirectoryName(filePath);
            if (pathToFile is null)
            {
                errorMessage = $"No parent directory found for \"{filePath}\"";
                return false;
            }
            File.Move(filePath, Path.Combine(pathToFile, newName));
            errorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public bool RenameDirAt(string dirPath, string newName, out string? errorMessage)
    {
        try
        {
            string? pathToDir = Path.GetDirectoryName(dirPath);
            if (pathToDir is null)
            {
                errorMessage = $"\"{dirPath}\" is a root directory";
                return false;
            }
            Directory.Move(dirPath, Path.Combine(pathToDir, newName));
            errorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public bool MoveFileTo(string filePath, string destinationDir, out string? errorMessage)
    {
        try
        {
            string newFilePath = Path.Combine(destinationDir, Path.GetFileName(filePath));
            File.Move(filePath, newFilePath);
            errorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public bool MoveDirTo(string dirPath, string destinationDir, out string? errorMessage)
    {
        try
        {
            string newDirPath = Path.Combine(destinationDir, Path.GetFileName(dirPath));
            Directory.Move(dirPath, newDirPath);
            errorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public bool CopyFileTo(string filePath, string copyName, string destinationDir, out string? errorMessage)
    {
        try
        {
            errorMessage = null;
            File.Copy(filePath, Path.Combine(destinationDir, copyName));
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public bool CopyDirTo(string dirPath, string copyName, string destinationDir, out string? errorMessage)
    {
        try
        {
            errorMessage = null;
            FileSystem.CopyDirectory(dirPath, Path.Combine(destinationDir, copyName));
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public bool ShowProperties(string filePath, out string? errorMessage) =>
        WindowsFileProperties.ShowFileProperties(filePath, out errorMessage);

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
