using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualBasic.FileIO;

namespace FileSurfer;

class WindowsFileOperationsHandler : IFileOperationsHandler
{
    // 250 MiB
    private const int ShowDialogLimit = 262144000;
    private const string NewImgName = "Image.png";

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
            if (!Directory.Exists(dirPath))
            {
                errorMessage = $"Could not find directory: \"{dirPath}\"";
                return false;
            }
            Directory.Delete(dirPath, true);
            errorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public DriveInfo[] GetDrives()
    {
        try
        {
            return DriveInfo.GetDrives();
        }
        catch
        {
            return Array.Empty<DriveInfo>();
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
                    (!includeHidden && IsHidden(files[i], false))
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
                    (!includeHidden && IsHidden(directories[i], true))
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
        bool showDialog = GetFileSizeB(filePath) > ShowDialogLimit;
        try
        {
            FileSystem.DeleteFile(
                filePath,
                showDialog ? UIOption.AllDialogs : UIOption.OnlyErrorDialogs,
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
        WindowsFileRestorer.RestoreFile(ogFilePath, out errorMessage);

    public bool RestoreDir(string ogDirPath, out string? errorMessage) =>
        WindowsFileRestorer.RestoreDir(ogDirPath, out errorMessage);

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

    [STAThread]
    public bool CopyToOSClipBoard(string[] paths, out string? errorMessage)
    {
        try
        {
            StringCollection fileCollection = new();
            fileCollection.AddRange(paths);
            Clipboard.SetFileDropList(fileCollection);
            errorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    [STAThread]
    public bool PasteFromOSClipBoard(string destinationPath, out string? errorMessage)
    {
        errorMessage = null;
        if (Clipboard.ContainsImage())
        {
            SaveImageToPath(destinationPath, out errorMessage);
            return false;
        }
        if (!Clipboard.ContainsFileDropList())
            return true;

        try
        {
            StringCollection fileCollection = Clipboard.GetFileDropList();
            foreach (string? filePath in fileCollection)
            {
                if (filePath is null)
                    throw new ArgumentNullException(filePath);

                if (File.Exists(filePath))
                    CopyFileTo(filePath, destinationPath, out errorMessage);
                else if (Directory.Exists(filePath))
                    CopyDirTo(filePath, destinationPath, out errorMessage);
            }
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    [STAThread]
    private static void SaveImageToPath(string destinationPath, out string? errorMessage)
    {
        errorMessage = null;
        if (Clipboard.GetImage() is not Image image)
            return;

        string imgName = FileNameGenerator.GetAvailableName(destinationPath, NewImgName);
        try
        {
            image.Save(Path.Combine(destinationPath, imgName), ImageFormat.Png);
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
        }
        image.Dispose();
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

        errorMessage = null;
        return process.ExitCode == 0;
    }

    private static bool IsValidFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        foreach (char c in Path.GetInvalidFileNameChars())
        {
            if (fileName.Contains(c))
                return false;
        }
        return true;
    }

    private static bool IsValidDirName(string dirName)
    {
        if (string.IsNullOrWhiteSpace(dirName))
            return false;

        foreach (char c in Path.GetInvalidPathChars())
        {
            if (dirName.Contains(c))
                return false;
        }
        return true;
    }

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
        bool showDialog = GetFileSizeB(filePath) > ShowDialogLimit;
        try
        {
            string newFilePath = Path.Combine(destinationDir, Path.GetFileName(filePath));
            FileSystem.MoveFile(
                filePath,
                newFilePath,
                showDialog ? UIOption.AllDialogs : UIOption.OnlyErrorDialogs,
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
        bool showDialog = GetFileSizeB(filePath) > ShowDialogLimit;
        try
        {
            errorMessage = null;
            string newFilePath = Path.Combine(destinationDir, Path.GetFileName(filePath));
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
