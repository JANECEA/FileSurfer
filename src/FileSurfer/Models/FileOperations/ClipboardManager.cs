using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using FileSurfer.Models.FileInformation;

namespace FileSurfer.Models.FileOperations;

/// <summary>
/// Interacts with the program and system clipboards using <see cref="System.Windows.Forms"/>.
/// </summary>
public class ClipboardManager : IClipboardManager
{
    private readonly string _newImageName;
    private readonly IFileIOHandler _fileIOHandler;
    private IFileSystemEntry[] _programClipboard = Array.Empty<IFileSystemEntry>();
    private string _copyFromDir = string.Empty;

    public bool IsCutOperation { get; private set; }

    public ClipboardManager(IFileIOHandler fileIOHandler, string newImageName)
    {
        _newImageName = newImageName;
        _fileIOHandler = fileIOHandler;
    }

    [STAThread]
    private static void ClearClipboard() => Clipboard.Clear();

    [STAThread]
    private static FileOperationResult CopyToOSClipboard(string[] paths)
    {
        try
        {
            StringCollection fileCollection = new();
            fileCollection.AddRange(paths);
            Clipboard.SetFileDropList(fileCollection);
            return FileOperationResult.Ok();
        }
        catch (Exception ex)
        {
            Clipboard.Clear();
            return FileOperationResult.Error(ex.Message);
        }
    }

    [STAThread]
    private FileOperationResult PasteFromOSClipboard(string destinationPath)
    {
        if (!Clipboard.ContainsFileDropList())
            return FileOperationResult.Ok();

        try
        {
            StringCollection fileCollection = Clipboard.GetFileDropList();
            FileOperationResult result = FileOperationResult.Ok();
            foreach (string? path in fileCollection)
            {
                if (path is null)
                    throw new ArgumentNullException(path);

                if (Directory.Exists(path))
                    result.AddResult(_fileIOHandler.CopyDirTo(path, destinationPath));
                else if (File.Exists(path))
                    result.AddResult(_fileIOHandler.CopyFileTo(path, destinationPath));
            }
            return result;
        }
        catch (Exception ex)
        {
            return FileOperationResult.Error(ex.Message);
        }
    }

    [STAThread]
    private FileOperationResult SaveImageToPath(string destinationPath)
    {
        if (Clipboard.GetImage() is not Image image)
            return FileOperationResult.Error("There is no image in the system clipboard");

        string imgName = FileNameGenerator.GetAvailableName(destinationPath, _newImageName);
        try
        {
            image.Save(Path.Combine(destinationPath, imgName), ImageFormat.Png);
            return FileOperationResult.Ok();
        }
        catch (Exception ex)
        {
            return FileOperationResult.Error(ex.Message);
        }
        finally
        {
            image.Dispose();
        }
    }

    [STAThread]
    private bool CompareClipboards()
    {
        if (Clipboard.GetFileDropList() is not StringCollection filePaths)
            return false;

        foreach (IFileSystemEntry entry in _programClipboard)
            if (!filePaths.Contains(entry.PathToEntry))
                return false;

        return true;
    }

    [STAThread]
    public void CopyPathToFile(string filePath) => Clipboard.SetText('\"' + filePath + '\"');

    public IFileSystemEntry[] GetClipboard() => _programClipboard.ToArray();

    public IFileOperationResult Cut(IFileSystemEntry[] selectedFiles, string currentDir)
    {
        FileOperationResult result = CopyToOSClipboard(
            selectedFiles.Select(entry => entry.PathToEntry).ToArray()
        );
        if (result.IsOK)
        {
            _copyFromDir = currentDir;
            IsCutOperation = true;
            _programClipboard = selectedFiles;
        }
        return result;
    }

    public IFileOperationResult Copy(IFileSystemEntry[] selectedFiles, string currentDir)
    {
        FileOperationResult result = CopyToOSClipboard(
            selectedFiles.Select(entry => entry.PathToEntry).ToArray()
        );
        if (result.IsOK)
        {
            _copyFromDir = currentDir;
            IsCutOperation = false;
            _programClipboard = selectedFiles;
        }
        return result;
    }

    public bool IsDuplicateOperation(string currentDir) =>
        !IsCutOperation && _copyFromDir == currentDir && _programClipboard.Length > 0;

    public IFileOperationResult Paste(string currentDir)
    {
        if (FileSurferSettings.AllowImagePastingFromClipboard && Clipboard.ContainsImage())
        {
            SaveImageToPath(currentDir);
            return NoMessageResult.Error();
        }
        FileOperationResult result = PasteFromOSClipboard(currentDir);
        if (!result.IsOK)
        {
            _programClipboard = Array.Empty<IFileSystemEntry>();
            return result;
        }

        IsCutOperation = IsCutOperation && CompareClipboards();
        if (IsCutOperation)
        {
            foreach (IFileSystemEntry entry in _programClipboard)
                result.AddResult(
                    entry is DirectoryEntry
                        ? _fileIOHandler.DeleteDir(entry.PathToEntry)
                        : _fileIOHandler.DeleteFile(entry.PathToEntry)
                );

            _programClipboard = Array.Empty<IFileSystemEntry>();
            Clipboard.Clear();
        }
        return result;
    }

    public IFileOperationResult Duplicate(string currentDir, out string[] copyNames)
    {
        copyNames = new string[_programClipboard.Length];
        FileOperationResult result = FileOperationResult.Ok();
        for (int i = 0; i < _programClipboard.Length; i++)
        {
            IFileSystemEntry entry = _programClipboard[i];
            copyNames[i] = FileNameGenerator.GetCopyName(currentDir, entry);

            result.AddResult(
                entry is DirectoryEntry
                    ? _fileIOHandler.DuplicateDir(entry.PathToEntry, copyNames[i])
                    : _fileIOHandler.DuplicateFile(entry.PathToEntry, copyNames[i])
            );
        }
        if (!result.IsOK)
        {
            ClearClipboard();
            _programClipboard = Array.Empty<IFileSystemEntry>();
        }
        return result;
    }
}
