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
    private readonly IFileInfoProvider _fileInfoProvider;
    private readonly IFileRestorer _fileRestorer;
    private readonly long _showDialogLimit;

    public WindowsFileIOHandler(
        IFileInfoProvider fileInfoProvider,
        IFileRestorer fileRestorer,
        long showDialogLimit
    )
    {
        _fileInfoProvider = fileInfoProvider;
        _fileRestorer = fileRestorer;
        _showDialogLimit = showDialogLimit;
    }

    public IFileOperationResult DeleteFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return FileOperationResult.Error($"Could not find file: \"{filePath}\"");

            FileSystem.DeleteFile(
                filePath,
                UIOption.OnlyErrorDialogs,
                RecycleOption.DeletePermanently,
                UICancelOption.ThrowException
            );
            return FileOperationResult.Ok();
        }
        catch (Exception ex)
        {
            return FileOperationResult.Error(ex.Message);
        }
    }

    public IFileOperationResult DeleteDir(string dirPath)
    {
        try
        {
            if (!Directory.Exists(dirPath))
                return FileOperationResult.Error($"Could not find directory: \"{dirPath}\"");

            FileSystem.DeleteDirectory(
                dirPath,
                UIOption.OnlyErrorDialogs,
                RecycleOption.DeletePermanently,
                UICancelOption.ThrowException
            );
            return FileOperationResult.Ok();
        }
        catch (Exception ex)
        {
            return FileOperationResult.Error(ex.Message);
        }
    }

    public IFileOperationResult MoveFileToTrash(string filePath)
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
            return FileOperationResult.Ok();
        }
        catch (Exception ex)
        {
            return FileOperationResult.Error(ex.Message);
        }
    }

    public IFileOperationResult MoveDirToTrash(string dirPath)
    {
        try
        {
            FileSystem.DeleteDirectory(
                dirPath,
                UIOption.AllDialogs,
                RecycleOption.SendToRecycleBin,
                UICancelOption.ThrowException
            );
            return FileOperationResult.Ok();
        }
        catch (Exception ex)
        {
            return FileOperationResult.Error(ex.Message);
        }
    }

    public IFileOperationResult RestoreFile(string ogFilePath) =>
        _fileRestorer.RestoreFile(ogFilePath);

    public IFileOperationResult RestoreDir(string ogDirPath) => _fileRestorer.RestoreDir(ogDirPath);

    public IFileOperationResult NewFileAt(string dirPath, string fileName)
    {
        if (!IsValidFileName(fileName))
            return FileOperationResult.Error($"File name: \"{fileName}\" is invalid.");

        try
        {
            string filePath = Path.Combine(dirPath, fileName);
            if (File.Exists(filePath))
                return FileOperationResult.Error($"File: \"{filePath}\" already exists");

            using FileStream file = File.Create(filePath);
            file.Close();

            return FileOperationResult.Ok();
        }
        catch (Exception ex)
        {
            return FileOperationResult.Error(ex.Message);
        }
    }

    public IFileOperationResult NewDirAt(string dirPath, string dirName)
    {
        if (!IsValidDirName(dirName))
            return FileOperationResult.Error($"Directory name: \"{dirName}\" is invalid.");

        try
        {
            string newDirPath = Path.Combine(dirPath, dirName);
            if (Directory.Exists(newDirPath))
                return FileOperationResult.Error($"Directory: \"{newDirPath}\" already exists");

            Directory.CreateDirectory(newDirPath);

            return FileOperationResult.Ok();
        }
        catch (Exception ex)
        {
            return FileOperationResult.Error(ex.Message);
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

    public IFileOperationResult RenameFileAt(string filePath, string newName)
    {
        if (!IsValidFileName(newName))
            return FileOperationResult.Error($"File name: \"{newName}\" is invalid.");

        try
        {
            if (Path.GetDirectoryName(filePath) is not string parentDir)
                return FileOperationResult.Error($"No parent directory found for \"{filePath}\"");

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

            return FileOperationResult.Ok();
        }
        catch (Exception ex)
        {
            return FileOperationResult.Error(ex.Message);
        }
    }

    public IFileOperationResult RenameDirAt(string dirPath, string newName)
    {
        if (!IsValidDirName(newName))
            return FileOperationResult.Error($"Directory name: \"{newName}\" is invalid.");

        try
        {
            if (Path.GetDirectoryName(dirPath) is not string parentDir)
                return FileOperationResult.Error($"\"{dirPath}\" is a root directory");

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

            return FileOperationResult.Ok();
        }
        catch (Exception ex)
        {
            return FileOperationResult.Error(ex.Message);
        }
    }

    public IFileOperationResult MoveFileTo(string filePath, string destinationDir)
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
            return FileOperationResult.Ok();
        }
        catch (Exception ex)
        {
            return FileOperationResult.Error(ex.Message);
        }
    }

    public IFileOperationResult MoveDirTo(string dirPath, string destinationDir)
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
            return FileOperationResult.Ok();
        }
        catch (Exception ex)
        {
            return FileOperationResult.Error(ex.Message);
        }
    }

    public IFileOperationResult CopyFileTo(string filePath, string destinationDir)
    {
        try
        {
            string newFilePath = Path.Combine(destinationDir, Path.GetFileName(filePath));
            FileSystem.CopyFile(
                filePath,
                newFilePath,
                UIOption.AllDialogs,
                UICancelOption.ThrowException
            );
            return FileOperationResult.Ok();
        }
        catch (Exception ex)
        {
            return FileOperationResult.Error(ex.Message);
        }
    }

    public IFileOperationResult CopyDirTo(string dirPath, string destinationDir)
    {
        try
        {
            string newDirPath = Path.Combine(destinationDir, Path.GetFileName(dirPath));
            FileSystem.CopyDirectory(
                dirPath,
                newDirPath,
                UIOption.AllDialogs,
                UICancelOption.ThrowException
            );
            return FileOperationResult.Ok();
        }
        catch (Exception ex)
        {
            return FileOperationResult.Error(ex.Message);
        }
    }

    public IFileOperationResult DuplicateFile(string filePath, string copyName)
    {
        bool showDialog = _fileInfoProvider.GetFileSizeB(filePath) > _showDialogLimit;
        try
        {
            if (Path.GetDirectoryName(filePath) is not string parentDir)
                return FileOperationResult.Error("Can't duplicate a root directory.");

            string newFilePath = Path.Combine(parentDir, copyName);
            FileSystem.CopyFile(
                filePath,
                newFilePath,
                showDialog ? UIOption.AllDialogs : UIOption.OnlyErrorDialogs,
                UICancelOption.ThrowException
            );
            return FileOperationResult.Ok();
        }
        catch (Exception ex)
        {
            return FileOperationResult.Error(ex.Message);
        }
    }

    public IFileOperationResult DuplicateDir(string dirPath, string copyName)
    {
        try
        {
            if (Path.GetDirectoryName(dirPath) is not string parentDir)
                return FileOperationResult.Error("Can't duplicate a root directory.");

            string newDirPath = Path.Combine(parentDir, copyName);
            FileSystem.CopyDirectory(
                dirPath,
                newDirPath,
                UIOption.AllDialogs,
                UICancelOption.ThrowException
            );
            return FileOperationResult.Ok();
        }
        catch (Exception ex)
        {
            return FileOperationResult.Error(ex.Message);
        }
    }
}
