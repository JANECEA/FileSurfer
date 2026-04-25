using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FileSurfer.Core.Extensions;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileInformation;
using FileSurfer.Core.Services.Dialogs;
using FileSurfer.Core.Services.FileOperations;
using Microsoft.VisualBasic.FileIO;

namespace FileSurfer.Windows.Services.FileOperations;

/// <summary>
/// Handles file IO operations in the Windows environment within the context of the <see cref="FileSurfer"/> app.
/// </summary>
public class WindowsFileIoHandler : IFileIoHandler
{
    private readonly IFileInfoProvider _fileInfoProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowsFileIoHandler"/> class.
    /// </summary>
    /// <param name="fileInfoProvider">The file info provider used for name collision checks.</param>
    public WindowsFileIoHandler(IFileInfoProvider fileInfoProvider) =>
        _fileInfoProvider = fileInfoProvider;

    public IResult DeleteFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return SimpleResult.Error($"Could not find file: \"{filePath}\"");

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
                return SimpleResult.Error($"Could not find directory: \"{dirPath}\"");

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

    public async Task<IResult> WriteFileStreamAsync(
        FileTransferStream fileStream,
        string dirPath,
        ProgressReporter reporter,
        CancellationToken ct
    )
    {
        string filePath = LocalPathTools.Combine(dirPath, fileStream.Name);
        IResult result;
        try
        {
            await using FileStream writeStream = File.Open(filePath, FileMode.Create);
            result = await fileStream.WriteToStreamAsync(writeStream, reporter, ct);
        }
        catch (Exception ex)
        {
            result = SimpleResult.Error(ex.Message);
        }
        if (!result.IsOk)
            _ = DeleteFile(filePath);

        return result;
    }

    public Task<IResult> WriteDirStreamAsync(
        DirTransferStream dirStream,
        string dirPath,
        ProgressReporter reporter,
        CancellationToken ct
    ) => dirStream.WriteWithIoHandlerAsync(this, LocalPathTools.Instance, dirPath, reporter, ct);

    public IResult NewFileAt(string dirPath, string fileName)
    {
        if (!IsValidFileName(fileName))
            return SimpleResult.Error($"File name: \"{fileName}\" is invalid.");

        try
        {
            string filePath = Path.Combine(dirPath, fileName);
            if (File.Exists(filePath))
                return SimpleResult.Error($"File: \"{filePath}\" already exists");

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
            return SimpleResult.Error($"Directory name: \"{dirName}\" is invalid.");

        try
        {
            string newDirPath = Path.Combine(dirPath, dirName);
            if (Directory.Exists(newDirPath))
                return SimpleResult.Error($"Directory: \"{newDirPath}\" already exists");

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
            .All(ch => !fileName.Contains(ch, StringComparison.OrdinalIgnoreCase));

    private static bool IsValidDirName(string dirName) =>
        !string.IsNullOrWhiteSpace(dirName)
        && Path.GetInvalidPathChars()
            .All(ch => !dirName.Contains(ch, StringComparison.OrdinalIgnoreCase));

    public IResult RenameFileAt(string filePath, string newName)
    {
        if (!IsValidFileName(newName))
            return SimpleResult.Error($"File name: \"{newName}\" is invalid.");

        try
        {
            if (Path.GetDirectoryName(filePath) is not string parentDir)
                return SimpleResult.Error($"No parent directory found for \"{filePath}\"");

            string fileName = Path.GetFileName(filePath);
            if (string.Equals(fileName, newName, StringComparison.OrdinalIgnoreCase))
            {
                string tempName = FileNameGenerator.GetAvailableName(
                    _fileInfoProvider,
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
                UIOption.OnlyErrorDialogs,
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
            return SimpleResult.Error($"Directory name: \"{newName}\" is invalid.");

        try
        {
            if (Path.GetDirectoryName(dirPath) is not string parentDir)
                return SimpleResult.Error($"\"{dirPath}\" is a root directory");

            string dirName = Path.GetFileName(dirPath);
            if (string.Equals(dirName, newName, StringComparison.OrdinalIgnoreCase))
            {
                string tempName = FileNameGenerator.GetAvailableName(
                    _fileInfoProvider,
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
                UIOption.OnlyErrorDialogs,
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
            string newFilePath = Path.Combine(destinationDir, Path.GetFileName(filePath));
            FileSystem.MoveFile(
                filePath,
                newFilePath,
                UIOption.OnlyErrorDialogs,
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
            string newDirPath = Path.Combine(destinationDir, Path.GetFileName(dirPath));
            FileSystem.MoveDirectory(
                dirPath,
                newDirPath,
                UIOption.OnlyErrorDialogs,
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
            string newFilePath = Path.Combine(destinationDir, Path.GetFileName(filePath));
            FileSystem.CopyFile(
                filePath,
                newFilePath,
                UIOption.OnlyErrorDialogs,
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
            string newDirPath = Path.Combine(destinationDir, Path.GetFileName(dirPath));
            FileSystem.CopyDirectory(
                dirPath,
                newDirPath,
                UIOption.OnlyErrorDialogs,
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
        try
        {
            if (Path.GetDirectoryName(filePath) is not string parentDir)
                return SimpleResult.Error("Can't duplicate a root directory.");

            string newFilePath = Path.Combine(parentDir, copyName);
            FileSystem.CopyFile(
                filePath,
                newFilePath,
                UIOption.OnlyErrorDialogs,
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
                UIOption.OnlyErrorDialogs,
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
