using System;
using System.IO;
using System.Linq;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileInformation;
using FileSurfer.Core.Models.FileOperations;
using Microsoft.VisualBasic.FileIO;

namespace FileSurfer.Linux.Models.FileOperations;

/// <summary>
/// Handles file IO operations in the Windows environment within the context of the <see cref="FileSurfer"/> app.
/// </summary>
public class LinuxFileIOHandler : IFileIOHandler
{
    private readonly IFileInfoProvider _fileInfoProvider;
    private readonly long _showDialogLimit;

    public LinuxFileIOHandler(
        IFileInfoProvider fileInfoProvider,
        long showDialogLimit
    )
    {
        _fileInfoProvider = fileInfoProvider;
        _showDialogLimit = showDialogLimit;
    }

    public IResult DeleteFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return SimpleResult.Error(
                    $"Could not find file: \"{filePath}\""
                );

            FileSystem.DeleteFile(
                filePath,
                UIOption.OnlyErrorDialogs,
                RecycleOption.DeletePermanently,
                UICancelOption.ThrowException
            );
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }

    public IResult DeleteDir(string dirPath)
    {
        try
        {
            if (!Directory.Exists(dirPath))
                return SimpleResult.Error(
                    $"Could not find directory: \"{dirPath}\""
                );

            FileSystem.DeleteDirectory(
                dirPath,
                UIOption.OnlyErrorDialogs,
                RecycleOption.DeletePermanently,
                UICancelOption.ThrowException
            );
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }

    public IResult NewFileAt(string dirPath, string fileName)
    {
        if (!IsValidFileName(fileName))
            return SimpleResult.Error($"File name: \"{fileName}\" is invalid.");

        try
        {
            string filePath = Path.Combine(dirPath, fileName);
            if (File.Exists(filePath))
                return SimpleResult.Error(
                    $"File: \"{filePath}\" already exists"
                );

            using FileStream file = File.Create(filePath);
            file.Close();

            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }

    public IResult NewDirAt(string dirPath, string dirName)
    {
        if (!IsValidDirName(dirName))
            return SimpleResult.Error(
                $"Directory name: \"{dirName}\" is invalid."
            );

        try
        {
            string newDirPath = Path.Combine(dirPath, dirName);
            if (Directory.Exists(newDirPath))
                return SimpleResult.Error(
                    $"Directory: \"{newDirPath}\" already exists"
                );

            Directory.CreateDirectory(newDirPath);

            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }

    private static bool IsValidFileName(string fileName) =>
        !string.IsNullOrWhiteSpace(fileName)
        && Path.GetInvalidFileNameChars()
            .All(ch =>
                !fileName.Contains(ch, StringComparison.OrdinalIgnoreCase)
            );

    private static bool IsValidDirName(string dirName) =>
        !string.IsNullOrWhiteSpace(dirName)
        && Path.GetInvalidPathChars()
            .All(ch =>
                !dirName.Contains(ch, StringComparison.OrdinalIgnoreCase)
            );

    public IResult RenameFileAt(string filePath, string newName)
    {
        if (!IsValidFileName(newName))
            return SimpleResult.Error($"File name: \"{newName}\" is invalid.");

        try
        {
            if (Path.GetDirectoryName(filePath) is not string parentDir)
                return SimpleResult.Error(
                    $"No parent directory found for \"{filePath}\""
                );

            string fileName = Path.GetFileName(filePath);
            if (
                string.Equals(
                    fileName,
                    newName,
                    StringComparison.OrdinalIgnoreCase
                )
            )
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

            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }

    public IResult RenameDirAt(string dirPath, string newName)
    {
        if (!IsValidDirName(newName))
            return SimpleResult.Error(
                $"Directory name: \"{newName}\" is invalid."
            );

        try
        {
            if (Path.GetDirectoryName(dirPath) is not string parentDir)
                return SimpleResult.Error($"\"{dirPath}\" is a root directory");

            string dirName = Path.GetFileName(dirPath);
            if (
                string.Equals(
                    dirName,
                    newName,
                    StringComparison.OrdinalIgnoreCase
                )
            )
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

            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }

    public IResult MoveFileTo(string filePath, string destinationDir)
    {
        try
        {
            string newFilePath = Path.Combine(
                destinationDir,
                Path.GetFileName(filePath)
            );
            FileSystem.MoveFile(
                filePath,
                newFilePath,
                UIOption.AllDialogs,
                UICancelOption.ThrowException
            );
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }

    public IResult MoveDirTo(string dirPath, string destinationDir)
    {
        try
        {
            string newDirPath = Path.Combine(
                destinationDir,
                Path.GetFileName(dirPath)
            );
            FileSystem.MoveDirectory(
                dirPath,
                newDirPath,
                UIOption.AllDialogs,
                UICancelOption.ThrowException
            );
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }

    public IResult CopyFileTo(string filePath, string destinationDir)
    {
        try
        {
            string newFilePath = Path.Combine(
                destinationDir,
                Path.GetFileName(filePath)
            );
            FileSystem.CopyFile(
                filePath,
                newFilePath,
                UIOption.AllDialogs,
                UICancelOption.ThrowException
            );
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }

    public IResult CopyDirTo(string dirPath, string destinationDir)
    {
        try
        {
            string newDirPath = Path.Combine(
                destinationDir,
                Path.GetFileName(dirPath)
            );
            FileSystem.CopyDirectory(
                dirPath,
                newDirPath,
                UIOption.AllDialogs,
                UICancelOption.ThrowException
            );
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }

    public IResult DuplicateFile(string filePath, string copyName)
    {
        bool showDialog =
            _fileInfoProvider.GetFileSizeB(filePath) > _showDialogLimit;
        try
        {
            if (Path.GetDirectoryName(filePath) is not string parentDir)
                return SimpleResult.Error("Can't duplicate a root directory.");

            string newFilePath = Path.Combine(parentDir, copyName);
            FileSystem.CopyFile(
                filePath,
                newFilePath,
                showDialog ? UIOption.AllDialogs : UIOption.OnlyErrorDialogs,
                UICancelOption.ThrowException
            );
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }

    public IResult DuplicateDir(string dirPath, string copyName)
    {
        try
        {
            if (Path.GetDirectoryName(dirPath) is not string parentDir)
                return SimpleResult.Error("Can't duplicate a root directory.");

            string newDirPath = Path.Combine(parentDir, copyName);
            FileSystem.CopyDirectory(
                dirPath,
                newDirPath,
                UIOption.AllDialogs,
                UICancelOption.ThrowException
            );
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }
}
