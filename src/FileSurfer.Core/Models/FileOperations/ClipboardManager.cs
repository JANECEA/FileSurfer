using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using FileSurfer.Core.Models.FileInformation;

namespace FileSurfer.Core.Models.FileOperations;

/// <summary>
/// Interacts with the program and system clipboards using <see cref="Avalonia.Input.Platform.IClipboard"/>.
/// </summary>
public class ClipboardManager : IClipboardManager
{
    private readonly string _newImageName;
    private readonly IFileIoHandler _fileIoHandler;
    private readonly IClipboard _systemClipboard;
    private readonly IStorageProvider _storageProvider;

    private IFileSystemEntry[] _programClipboard = Array.Empty<IFileSystemEntry>();
    private string _copyFromDir = string.Empty;

    public bool IsCutOperation { get; private set; }

    public ClipboardManager(
        IClipboard clipboardManager,
        IStorageProvider storageProvider,
        IFileIoHandler fileIoHandler,
        string newImageName
    )
    {
        _newImageName = newImageName + ".png";
        _fileIoHandler = fileIoHandler;
        _systemClipboard = clipboardManager;
        _storageProvider = storageProvider;
    }

    public bool IsDuplicateOperation(string currentDir) =>
        !IsCutOperation && _copyFromDir == currentDir && _programClipboard.Length > 0;

    private async Task<SimpleResult> CopyToOsClipboardAsync(IFileSystemEntry[] entries)
    {
        try
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

            IEnumerable<IStorageItem> storageItems = files.Concat(folders);
            await _systemClipboard.SetFilesAsync(storageItems);
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            await _systemClipboard.ClearAsync();
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

    public async Task<IResult> CutAsync(IFileSystemEntry[] selectedFiles, string currentDir)
    {
        SimpleResult result = await CopyToOsClipboardAsync(selectedFiles);
        if (result.IsOk)
        {
            _copyFromDir = currentDir;
            IsCutOperation = true;
            _programClipboard = selectedFiles;
        }
        return result;
    }

    public async Task<IResult> CopyAsync(IFileSystemEntry[] selectedFiles, string currentDir)
    {
        SimpleResult result = await CopyToOsClipboardAsync(selectedFiles);
        if (result.IsOk)
        {
            _copyFromDir = currentDir;
            IsCutOperation = false;
            _programClipboard = selectedFiles;
        }
        return result;
    }

    private async Task<IResult> PasteFromOsClipboard(string destinationPath)
    {
        if (await _systemClipboard.TryGetFilesAsync() is not IStorageItem[] items)
            return SimpleResult.Ok();

        Result result = Result.Ok();
        foreach (IStorageItem item in items)
        {
            string normalizedPath = PathTools.NormalizePath(item.Path.LocalPath);
            if (item is IStorageFolder)
                result.MergeResult(
                    IsCutOperation
                        ? _fileIoHandler.MoveDirTo(normalizedPath, destinationPath)
                        : _fileIoHandler.CopyDirTo(normalizedPath, destinationPath)
                );
            else if (item is IStorageFile)
                result.MergeResult(
                    IsCutOperation
                        ? _fileIoHandler.MoveFileTo(normalizedPath, destinationPath)
                        : _fileIoHandler.CopyFileTo(normalizedPath, destinationPath)
                );
        }
        return result;
    }

    public async Task<IResult> PasteAsync(string currentDir)
    {
        IResult result;
        if (
            FileSurferSettings.AllowImagePastingFromClipboard
            && await _systemClipboard.TryGetBitmapAsync() is Bitmap image
        )
        {
            result = SaveImageToPath(currentDir, image);
            return result.IsOk ? SimpleResult.Error() : result;
        }

        IsCutOperation = IsCutOperation && await CompareClipboards();
        result = await PasteFromOsClipboard(currentDir);
        if (IsCutOperation)
        {
            _programClipboard = Array.Empty<IFileSystemEntry>();
            await _systemClipboard.ClearAsync();
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
                    ? _fileIoHandler.DuplicateDir(entry.PathToEntry, copyNames[i])
                    : _fileIoHandler.DuplicateFile(entry.PathToEntry, copyNames[i])
            );
        }
        if (!result.IsOk)
        {
            _systemClipboard.ClearAsync().Wait();
            _programClipboard = Array.Empty<IFileSystemEntry>();
        }
        return result;
    }

    public async Task CopyPathToFileAsymc(string filePath) =>
        await _systemClipboard.SetTextAsync('\"' + filePath + '\"');

    public IFileSystemEntry[] GetClipboard() => _programClipboard.ToArray();
}
