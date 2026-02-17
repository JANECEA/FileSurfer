using System;
using System.IO;
using System.Linq;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileOperations;
using Microsoft.VisualBasic.FileIO;

namespace FileSurfer.Linux.Models.FileOperations;

/// <summary>
/// Handles file IO operations in the Linux environment.
/// </summary>
public class LinuxFileIoHandler : IFileIoHandler
{
    public IResult DeleteFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return SimpleResult.Error($"Could not find file: \"{filePath}\"");

            File.Delete(filePath);
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

            Directory.Delete(dirPath, true);
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
        && Path.GetInvalidFileNameChars().All(ch => !fileName.Contains(ch, PathTools.Comparison));

    private static bool IsValidDirName(string dirName) =>
        !string.IsNullOrWhiteSpace(dirName)
        && Path.GetInvalidPathChars().All(ch => !dirName.Contains(ch, PathTools.Comparison));

    public IResult RenameFileAt(string filePath, string newName)
    {
        if (!IsValidFileName(newName))
            return SimpleResult.Error($"File name: \"{newName}\" is invalid.");

        try
        {
            if (Path.GetDirectoryName(filePath) is not string parentDir)
                return SimpleResult.Error($"File \"{filePath}\" has no parent directory.");

            if (!string.Equals(Path.GetFileName(filePath), newName, PathTools.Comparison))
                File.Move(filePath, Path.Combine(parentDir, newName));

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

            if (!string.Equals(Path.GetFileName(dirPath), newName, PathTools.Comparison))
                Directory.Move(dirPath, Path.Combine(parentDir, newName));

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
            File.Move(filePath, newFilePath);
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
            Directory.Move(dirPath, newDirPath);
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
            File.Copy(filePath, newFilePath);
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
            FileSystem.CopyDirectory(dirPath, newDirPath, false);
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
            File.Copy(filePath, newFilePath);
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
            FileSystem.CopyDirectory(dirPath, newDirPath, false);
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }
}
