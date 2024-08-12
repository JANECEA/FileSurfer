using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using Microsoft.VisualBasic.FileIO;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Writers;

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

    public string[] GetDirFiles(string dirPath, out string? errorMessage)
    {
        try
        {
            errorMessage = null;
            return Directory.GetFiles(dirPath);
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return Array.Empty<string>();
        }
    }

    public string[] GetDirFolders(string dirPath, out string? errorMessage)
    {
        try
        {
            errorMessage = null;
            return Directory.GetDirectories(dirPath);
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
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
            FileSystem.DeleteDirectory(
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
            FileSystem.DeleteFile(
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

    public bool IsHidden(string filePath)
    {
        try
        {
            if (File.GetAttributes(filePath).HasFlag(FileAttributes.Directory))
            {
                return new DirectoryInfo(filePath).Attributes.HasFlag(FileAttributes.Hidden);
            }
            return new FileInfo(filePath).Attributes.HasFlag(FileAttributes.Hidden);
        }
        catch
        {
            return false;
        }
    }

    public string GetAvailableName(string path, string fileName)
    {
        if (!Path.Exists(Path.Combine(path, fileName)))
        {
            return fileName;
        }
        string nameWOextension = Path.GetFileNameWithoutExtension(fileName);
        string extension = Path.GetExtension(fileName);
        for (int index = 1; ; index++)
        {
            string newFileName = $"{nameWOextension} ({index}){extension}";
            if (!Path.Exists(Path.Combine(path, newFileName)))
            {
                return newFileName;
            }
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

    public bool CopyFileTo(string filePath, string destinationDir, out string? errorMessage)
    {
        try
        {
            errorMessage = null;
            if (Path.GetDirectoryName(filePath) == destinationDir)
            {
                string newName =
                    Path.GetFileNameWithoutExtension(filePath)
                    + " - copy"
                    + Path.GetExtension(filePath);
                newName = GetAvailableName(destinationDir, newName);
                File.Copy(filePath, Path.Combine(destinationDir, newName));
                return true;
            }
            string fileName = Path.GetFileName(filePath);
            File.Copy(filePath, Path.Combine(destinationDir, fileName), true);
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
            string dirName = Path.GetFileName(dirPath);
            if (Path.GetDirectoryName(dirPath) == destinationDir)
            {
                string newName = GetAvailableName(destinationDir, dirName + " - copy");
                FileSystem.CopyDirectory(dirPath, Path.Combine(destinationDir, newName));
                return true;
            }
            FileSystem.CopyDirectory(dirPath, Path.Combine(destinationDir, dirName), true);
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

    public bool IsZipped(string filePath) =>
        Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".zip" => true,
            ".rar" => true,
            ".7z" => true,
            ".gzip" => true,
            ".tar" => true,
            _ => false
        };

    public bool ZipFiles(string[] filePaths, string destinationPath, out string? errorMessage)
    {
        try
        {
            using ZipArchive archive = ZipArchive.Create();
            using FileStream zipStream = File.OpenWrite(destinationPath);

            foreach (string filePath in filePaths)
            {
                archive.AddEntry(Path.GetFileName(filePath), File.OpenRead(filePath));
            }
            archive.SaveTo(zipStream, new WriterOptions(CompressionType.Deflate));
            errorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public bool UnzipArchive(string archivePath, string extractPath, out string? errorMessage)
    {
        if (!IsZipped(archivePath))
        {
            errorMessage = $"\"{archivePath}\" is not an archive.";
            return false;
        }
        try
        {
            string extractName = Path.GetFileNameWithoutExtension(archivePath);
            Directory.CreateDirectory(Path.Combine(extractPath, extractName));
            using IArchive archive = ArchiveFactory.Open(archivePath);
            foreach (IArchiveEntry entry in archive.Entries.Where(entry => !entry.IsDirectory))
            {
                entry.WriteToDirectory(
                    extractPath,
                    new ExtractionOptions() { ExtractFullPath = true, Overwrite = true }
                );
            }
            errorMessage = null;
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
}
