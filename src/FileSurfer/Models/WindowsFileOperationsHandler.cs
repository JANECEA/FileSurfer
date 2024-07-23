using System;
using System.IO;
using System.Diagnostics;
using Microsoft.VisualBasic.FileIO;
// using System.Windows.Forms;

namespace FileSurfer;

class WindowsFileOperationsHandler : IFileOperationsHandler
{
    public bool DeleteFile(string filePath, out string? errorMessage)
    {
        try
        {
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

    public bool DeleteDirectory(string dirPath, out string? errorMessage)
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

    public object GetFileIcon(string path, out string? errorMessage) { throw new System.NotImplementedException(); }

    public object GetFileContextMenu(string path, out string? errorMessage) { throw new System.NotImplementedException(); }

    public long? GetFileSizeKiB(string path, out string? errorMessage)
    {
        try
        {
            errorMessage = null;
            return new FileInfo(path).Length / 1024;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return null;
        }
    }

    public bool MoveFileToTrash(string filePath, out string? errorMessage)
    {
        try
        {
            FileSystem.DeleteDirectory(filePath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
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
            FileSystem.DeleteFile(dirPath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            errorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public bool NewFileAt(string dirPath, string fileName, out string? errorMessage)
    {
        try
        {
            using FileStream file = File.Create(Path.Combine(dirPath, GetAvailableName(dirPath, fileName)));
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

    public bool NewDirAt(string dirPath, string folderName, out string? errorMessage)
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(dirPath, GetAvailableName(dirPath, folderName)));
            errorMessage = null; 
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    private static string GetAvailableName(string path, string fileName)
    {
        if (!Path.Exists(Path.Combine(path, fileName)))
        {
            return fileName;
        }
        for (int index = 1; ; index++)
        {
            string newFileName = $"{fileName} (${index})";
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
        throw new System.NotImplementedException(); 
    }

    public bool PasteFileFromClipBoardAt(string filePath, out string? errorMessage) { throw new System.NotImplementedException(); }

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
}
