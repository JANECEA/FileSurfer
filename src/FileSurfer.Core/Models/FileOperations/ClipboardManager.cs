using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using FileSurfer.Models.FileInformation;

namespace FileSurfer.Models.FileOperations;

/// <summary>
/// Interacts with the program and system clipboards using <see cref="System.Windows.Forms"/>.
/// </summary>
public class ClipboardManager : IClipboardManager
{
    private readonly string _newImageName;
    private readonly IFileIOHandler _fileIOHandler;
    private readonly IClipboard _systemClipboard;
    private readonly IStorageProvider _storageProvider;

    private IFileSystemEntry[] _programClipboard = Array.Empty<IFileSystemEntry>();
    private string _copyFromDir = string.Empty;

    public bool IsCutOperation { get; private set; }

    public ClipboardManager(
        IClipboard clipboardManager,
        IStorageProvider storageProvider,
        IFileIOHandler fileIOHandler,
        string newImageName
    )
    {
        _newImageName = newImageName + ".png";
        _fileIOHandler = fileIOHandler;
        _systemClipboard = clipboardManager;
        _storageProvider = storageProvider;
    }

    private void ClearSystemClipboard() => _systemClipboard.ClearAsync().Wait();

    private async Task<SimpleResult> CopyToOSClipboard(IFileSystemEntry[] entries)
    {
        List<Task<IStorageFile?>> fileTasks = new();
        List<Task<IStorageFolder?>> folderTasks = new();
        foreach (IFileSystemEntry entry in entries)
            if (entry is DirectoryEntry)
                folderTasks.Add(_storageProvider.TryGetFolderFromPathAsync(entry.PathToEntry));
            else if (entry is FileEntry)
                fileTasks.Add(_storageProvider.TryGetFileFromPathAsync(entry.PathToEntry));

        IEnumerable<IStorageItem> files = (await Task.WhenAll(fileTasks))
            .Where(file => file is not null)
            .Cast<IStorageItem>();
        IEnumerable<IStorageItem> folders = (await Task.WhenAll(folderTasks))
            .Where(folder => folder is not null)
            .Cast<IStorageItem>();

        try
        {
            IEnumerable<IStorageItem> storageItems = files.Concat(folders);
            await _systemClipboard.SetFilesAsync(storageItems);
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            ClearSystemClipboard();
            return SimpleResult.Error(ex.Message);
        }
    }

    private SimpleResult SaveImageToPath(string destinationPath, Bitmap image)
    {
        string imgName = FileNameGenerator.GetAvailableName(destinationPath, _newImageName);
        try
        {
            image.Save(Path.Combine(destinationPath, imgName));
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
        finally
        {
            image.Dispose();
        }
    }

    private async Task<bool> CompareClipboards()
    {
        if (
            await _systemClipboard.TryGetFilesAsync() is not IStorageItem[] items
            || items.Length != _programClipboard.Length
        )
            return false;

        HashSet<string> files = _programClipboard
            .Where(entry => entry is FileEntry)
            .Select(file => PathTools.NormalizePath(file.PathToEntry))
            .ToHashSet();
        HashSet<string> directories = _programClipboard
            .Where(entry => entry is DirectoryEntry)
            .Select(directory => PathTools.NormalizePath(directory.PathToEntry))
            .ToHashSet();

        foreach (IStorageItem item in items)
            if (item is IStorageFile)
            {
                if (!files.Contains(PathTools.NormalizePath(item.Path.LocalPath)))
                    return false;
            }
            else if (item is IStorageFolder)
            {
                if (!directories.Contains(PathTools.NormalizePath(item.Path.LocalPath)))
                    return false;
            }

        return true;
    }

    public void CopyPathToFile(string filePath) =>
        _systemClipboard.SetTextAsync('\"' + filePath + '\"').Wait();

    public IFileSystemEntry[] GetClipboard() => _programClipboard.ToArray();

    public IResult Cut(IFileSystemEntry[] selectedFiles, string currentDir)
    {
        SimpleResult result = CopyToOSClipboard(selectedFiles).Result;
        if (result.IsOk)
        {
            _copyFromDir = currentDir;
            IsCutOperation = true;
            _programClipboard = selectedFiles;
        }
        return result;
    }

    public IResult Copy(IFileSystemEntry[] selectedFiles, string currentDir)
    {
        SimpleResult result = CopyToOSClipboard(selectedFiles).Result;
        if (result.IsOk)
        {
            _copyFromDir = currentDir;
            IsCutOperation = false;
            _programClipboard = selectedFiles;
        }
        return result;
    }

    public bool IsDuplicateOperation(string currentDir) =>
        !IsCutOperation && _copyFromDir == currentDir && _programClipboard.Length > 0;

    private IResult PasteFromOSClipboard(string destinationPath)
    {
        if (_systemClipboard.TryGetFilesAsync().Result is not IStorageItem[] items)
            return SimpleResult.Ok();

        Result result = Result.Ok();
        foreach (IStorageItem item in items)
        {
            string normalizedPath = PathTools.NormalizePath(item.Path.LocalPath);
            if (item is IStorageFolder)
                result.MergeResult(
                    IsCutOperation
                        ? _fileIOHandler.MoveDirTo(normalizedPath, destinationPath)
                        : _fileIOHandler.CopyDirTo(normalizedPath, destinationPath)
                );
            else if (item is IStorageFile)
                result.MergeResult(
                    IsCutOperation
                        ? _fileIOHandler.MoveFileTo(normalizedPath, destinationPath)
                        : _fileIOHandler.CopyFileTo(normalizedPath, destinationPath)
                );
        }
        return result;
    }

    public IResult Paste(string currentDir)
    {
        if (
            FileSurferSettings.AllowImagePastingFromClipboard
            && _systemClipboard.TryGetBitmapAsync().Result is Bitmap image
        )
        {
            SaveImageToPath(currentDir, image);
            return SimpleResult.Error();
        }

        IsCutOperation = IsCutOperation && CompareClipboards().Result;
        IResult result = PasteFromOSClipboard(currentDir);
        if (IsCutOperation)
        {
            _programClipboard = Array.Empty<IFileSystemEntry>();
            ClearSystemClipboard();
        }

        if (!result.IsOk)
            _programClipboard = Array.Empty<IFileSystemEntry>();

        return result;
    }

    public IResult Duplicate(string currentDir, out string[] copyNames)
    {
        copyNames = new string[_programClipboard.Length];
        Result result = Result.Ok();
        for (int i = 0; i < _programClipboard.Length; i++)
        {
            IFileSystemEntry entry = _programClipboard[i];
            copyNames[i] = FileNameGenerator.GetCopyName(currentDir, entry);

            result.MergeResult(
                entry is DirectoryEntry
                    ? _fileIOHandler.DuplicateDir(entry.PathToEntry, copyNames[i])
                    : _fileIOHandler.DuplicateFile(entry.PathToEntry, copyNames[i])
            );
        }
        if (!result.IsOk)
        {
            ClearSystemClipboard();
            _programClipboard = Array.Empty<IFileSystemEntry>();
        }
        return result;
    }
}
