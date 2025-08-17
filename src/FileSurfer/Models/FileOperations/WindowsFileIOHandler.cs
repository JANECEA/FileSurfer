using System;
using System.IO;
using System.Linq;
using FileSurfer.Models.FileInformation;
using FileSurfer.Models.Shell;
using Microsoft.VisualBasic.FileIO;

namespace FileSurfer.Models.FileOperations;

/// <summary>
/// Handles file IO operations in the Windows environment within the context of the <see cref="FileSurfer"/> app.
/// </summary>
public class WindowsFileIOHandler : IFileIOHandler
{
    private readonly long _showDialogLimit;
    private readonly IFileInfoProvider _fileInfoProvider;

    public WindowsFileIOHandler(IFileInfoProvider fileInfoProvider, long showDialogLimit)
    {
        _fileInfoProvider = fileInfoProvider;
        _showDialogLimit = showDialogLimit;
    }

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

    public bool MoveFileToTrash(string filePath, out string? errorMessage)
    {
        try
        {
            FileSystem.DeleteFile(
                filePath,
                _fileInfoProvider.GetFileSizeB(filePath) > _showDialogLimit
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

    private static bool IsValidFileName(string fileName) =>
        !string.IsNullOrWhiteSpace(fileName)
        && Path.GetInvalidFileNameChars()
            .All(ch => !fileName.Contains(ch, StringComparison.OrdinalIgnoreCase));

    private static bool IsValidDirName(string dirName) =>
        !string.IsNullOrWhiteSpace(dirName)
        && Path.GetInvalidPathChars()
            .All(ch => !dirName.Contains(ch, StringComparison.OrdinalIgnoreCase));

    public bool RenameFileAt(string filePath, string newName, out string? errorMessage)
    {
        if (!IsValidFileName(newName))
        {
            errorMessage = $"File name: \"{newName}\" is invalid.";
            return false;
        }
        try
        {
            string? parentDir = Path.GetDirectoryName(filePath);
            if (parentDir is null)
            {
                errorMessage = $"No parent directory found for \"{filePath}\"";
                return false;
            }
            string dirName = Path.GetFileName(filePath);
            if (string.Equals(dirName, newName, StringComparison.OrdinalIgnoreCase))
            {
                string tempName = FileNameGenerator.GetAvailableName(
                    parentDir,
                    Path.GetRandomFileName()
                );
                string tempPath = Path.Combine(parentDir, tempName);
                File.Move(filePath, tempPath);
                filePath = tempPath;
            }
            FileSystem.MoveFile(
                filePath,
                Path.Combine(parentDir, newName),
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
            string? parentDir = Path.GetDirectoryName(dirPath);
            if (parentDir is null)
            {
                errorMessage = $"\"{dirPath}\" is a root directory";
                return false;
            }
            string dirName = Path.GetFileName(dirPath);
            if (string.Equals(dirName, newName, StringComparison.OrdinalIgnoreCase))
            {
                string tempName = FileNameGenerator.GetAvailableName(
                    parentDir,
                    Path.GetRandomFileName()
                );
                string tempPath = Path.Combine(parentDir, tempName);
                Directory.Move(dirPath, tempPath);
                dirPath = tempPath;
            }
            FileSystem.MoveDirectory(
                dirPath,
                Path.Combine(parentDir, newName),
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
        bool showDialog = _fileInfoProvider.GetFileSizeB(filePath) > _showDialogLimit;
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
}
