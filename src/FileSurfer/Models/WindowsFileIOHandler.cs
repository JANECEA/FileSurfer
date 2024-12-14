using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using Microsoft.VisualBasic.FileIO;

namespace FileSurfer.Models;

/// <summary>
/// Handles file IO operations in the Windows environment within the context of the <see cref="FileSurfer"/> app.
/// </summary>
public class WindowsFileIOHandler : IFileIOHandler
{
    private readonly long _showDialogLimit;

    public WindowsFileIOHandler(long showDialogLimit) => _showDialogLimit = showDialogLimit;

    public bool DeleteFile(string filePath, out string? errorMessage)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                errorMessage = $"Could not find file: \"{filePath}\"";
                return false;
            }
            FileSystem.DeleteFile(
                filePath,
                UIOption.OnlyErrorDialogs,
                RecycleOption.DeletePermanently,
                UICancelOption.ThrowException
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

    public bool DeleteDir(string dirPath, out string? errorMessage)
    {
        try
        {
            if (!Directory.Exists(dirPath))
            {
                errorMessage = $"Could not find directory: \"{dirPath}\"";
                return false;
            }
            FileSystem.DeleteDirectory(
                dirPath,
                UIOption.OnlyErrorDialogs,
                RecycleOption.DeletePermanently,
                UICancelOption.ThrowException
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

    public DriveInfo[] GetDrives() =>
        DriveInfo
            .GetDrives()
            .Where(drive =>
            {
                try
                {
                    _ = drive.Name + drive.VolumeLabel + drive.TotalSize.ToString();
                    return drive.IsReady;
                }
                catch
                {
                    return false;
                }
            })
            .ToArray();

    public string[] GetSpecialFolders()
    {
        try
        {
            return new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads",
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            };
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public string[] GetPathFiles(string path, bool includeHidden, bool includeOS)
    {
        try
        {
            string[] files = Directory.GetFiles(path);

            if (includeHidden && includeOS)
                return files;

            for (int i = 0; i < files.Length; i++)
            {
                if (
                    !includeHidden && IsHidden(files[i], false)
                    || !includeOS && IsOSProtected(files[i], false)
                )
                    files[i] = string.Empty;
            }
            return files.Where(filePath => filePath != string.Empty).ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public string[] GetPathDirs(string path, bool includeHidden, bool includeOS)
    {
        try
        {
            string[] directories = Directory.GetDirectories(path);

            if (includeHidden && includeOS)
                return directories;

            for (int i = 0; i < directories.Length; i++)
            {
                if (
                    !includeHidden && IsHidden(directories[i], true)
                    || !includeOS && IsOSProtected(directories[i], true)
                )
                    directories[i] = string.Empty;
            }
            return directories.Where(dirPath => dirPath != string.Empty).ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public Bitmap? GetFileIcon(string path)
    {
        try
        {
            return Icon.ExtractAssociatedIcon(path)?.ToBitmap();
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
                GetFileSizeB(filePath) > _showDialogLimit
                    ? UIOption.AllDialogs
                    : UIOption.OnlyErrorDialogs,
                RecycleOption.SendToRecycleBin,
                UICancelOption.ThrowException
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
                UIOption.AllDialogs,
                RecycleOption.SendToRecycleBin,
                UICancelOption.ThrowException
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
        WindowsFileRestorer.RestoreEntry(ogFilePath, out errorMessage);

    public bool RestoreDir(string ogDirPath, out string? errorMessage) =>
        WindowsFileRestorer.RestoreEntry(ogDirPath, out errorMessage);

    public bool NewFileAt(string dirPath, string fileName, out string? errorMessage)
    {
        if (!IsValidFileName(fileName))
        {
            errorMessage = $"File name: \"{fileName}\" is invalid.";
            return false;
        }
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
        if (!IsValidDirName(dirName))
        {
            errorMessage = $"Directory name: \"{dirName}\" is invalid.";
            return false;
        }
        try
        {
            string newDirPath = Path.Combine(dirPath, dirName);
            if (Directory.Exists(newDirPath))
            {
                errorMessage = $"Directory: \"{newDirPath}\" already exists";
                return false;
            }
            Directory.CreateDirectory(newDirPath);
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
            return isDirectory
                ? new DirectoryInfo(path).Attributes.HasFlag(FileAttributes.Hidden)
                : new FileInfo(path).Attributes.HasFlag(FileAttributes.Hidden);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsOSProtected(string path, bool isDirectory)
    {
        try
        {
            return isDirectory
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
                    FileName = FileSurferSettings.NotePadApp,
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

    private static bool IsValidFileName(string fileName) =>
        !string.IsNullOrWhiteSpace(fileName)
        && Path.GetInvalidFileNameChars().All(ch => !fileName.Contains(ch));

    private static bool IsValidDirName(string dirName) =>
        !string.IsNullOrWhiteSpace(dirName)
        && Path.GetInvalidPathChars().All(ch => !dirName.Contains(ch));

    public bool RenameFileAt(string filePath, string newName, out string? errorMessage)
    {
        if (!IsValidFileName(newName))
        {
            errorMessage = $"File name: \"{newName}\" is invalid.";
            return false;
        }
        try
        {
            string? pathToFile = Path.GetDirectoryName(filePath);
            if (pathToFile is null)
            {
                errorMessage = $"No parent directory found for \"{filePath}\"";
                return false;
            }
            FileSystem.MoveFile(
                filePath,
                Path.Combine(pathToFile, newName),
                UIOption.AllDialogs,
                UICancelOption.ThrowException
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

    public bool RenameDirAt(string dirPath, string newName, out string? errorMessage)
    {
        if (!IsValidDirName(newName))
        {
            errorMessage = $"Directory name: \"{newName}\" is invalid.";
            return false;
        }
        try
        {
            string? pathToDir = Path.GetDirectoryName(dirPath);
            if (pathToDir is null)
            {
                errorMessage = $"\"{dirPath}\" is a root directory";
                return false;
            }
            FileSystem.MoveDirectory(
                dirPath,
                Path.Combine(pathToDir, newName),
                UIOption.AllDialogs,
                UICancelOption.ThrowException
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

    public bool MoveFileTo(string filePath, string destinationDir, out string? errorMessage)
    {
        try
        {
            string newFilePath = Path.Combine(destinationDir, Path.GetFileName(filePath));
            FileSystem.MoveFile(
                filePath,
                newFilePath,
                UIOption.AllDialogs,
                UICancelOption.ThrowException
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

    public bool MoveDirTo(string dirPath, string destinationDir, out string? errorMessage)
    {
        try
        {
            string newDirPath = Path.Combine(destinationDir, Path.GetFileName(dirPath));
            FileSystem.MoveDirectory(
                dirPath,
                newDirPath,
                UIOption.AllDialogs,
                UICancelOption.ThrowException
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

    public bool CopyFileTo(string filePath, string destinationDir, out string? errorMessage)
    {
        try
        {
            errorMessage = null;
            string newFilePath = Path.Combine(destinationDir, Path.GetFileName(filePath));
            FileSystem.CopyFile(
                filePath,
                newFilePath,
                UIOption.AllDialogs,
                UICancelOption.ThrowException
            );
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public bool CopyDirTo(string dirPath, string destinationDir, out string? errorMessage)
    {
        try
        {
            errorMessage = null;
            string newDirPath = Path.Combine(destinationDir, Path.GetFileName(dirPath));
            FileSystem.CopyDirectory(
                dirPath,
                newDirPath,
                UIOption.AllDialogs,
                UICancelOption.ThrowException
            );
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public bool DuplicateFile(string filePath, string copyName, out string? errorMessage)
    {
        bool showDialog = GetFileSizeB(filePath) > _showDialogLimit;
        try
        {
            errorMessage = null;
            if (Path.GetDirectoryName(filePath) is not string parentDir)
            {
                errorMessage = "Can't duplicate a root directory.";
                return false;
            }
            string newFilePath = Path.Combine(parentDir, copyName);
            FileSystem.CopyFile(
                filePath,
                newFilePath,
                showDialog ? UIOption.AllDialogs : UIOption.OnlyErrorDialogs,
                UICancelOption.ThrowException
            );
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public bool DuplicateDir(string dirPath, string copyName, out string? errorMessage)
    {
        try
        {
            errorMessage = null;
            if (Path.GetDirectoryName(dirPath) is not string parentDir)
            {
                errorMessage = "Can't duplicate a root directory.";
                return false;
            }
            string newDirPath = Path.Combine(parentDir, copyName);
            FileSystem.CopyDirectory(
                dirPath,
                newDirPath,
                UIOption.AllDialogs,
                UICancelOption.ThrowException
            );
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
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

    public bool IsLinkedToDirectory(string linkPath, out string? directory)
    {
        directory = null;
        if (
            !Path.GetExtension(linkPath).Equals(".lnk", StringComparison.InvariantCultureIgnoreCase)
        )
            return false;

        try
        {
            IWshRuntimeLibrary.WshShell shell = new();
            IWshRuntimeLibrary.IWshShortcut shortcut = (IWshRuntimeLibrary.IWshShortcut)
                shell.CreateShortcut(linkPath);

            if (Directory.Exists(shortcut.TargetPath))
            {
                directory = shortcut.TargetPath;
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }
}
