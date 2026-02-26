using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using FileSurfer.Core.Extensions;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileInformation;

namespace FileSurfer.Core.Services.FileOperations;

/// <summary>
/// Interacts with the program and system clipboards using <see cref="Avalonia.Input.Platform.IClipboard"/>.
/// </summary>
public class LocalClipboardManager : ILocalClipboardManager
{
    private readonly IFileIoHandler _fileIoHandler;
    private readonly IFileInfoProvider _fileInfoProvider;
    private readonly IClipboard _systemClipboard;
    private readonly IStorageProvider _storageProvider;

    private IFileSystemEntry[] _programClipboard = Array.Empty<IFileSystemEntry>();
    private string _copyFromDir = string.Empty;
    private PasteType _pasteType = PasteType.Copy;

    public LocalClipboardManager(
        IClipboard clipboardManager,
        IStorageProvider storageProvider,
        IFileIoHandler fileIoHandler,
        IFileInfoProvider fileInfoProvider
    )
    {
        _fileIoHandler = fileIoHandler;
        _fileInfoProvider = fileInfoProvider;
        _systemClipboard = clipboardManager;
        _storageProvider = storageProvider;
    }

    public async Task<PasteType> GetOperationType(string currentDir)
    {
        if (_pasteType == PasteType.Copy && LocalPathTools.PathsAreEqual(_copyFromDir, currentDir))
            _pasteType = PasteType.Duplicate;

        if (_pasteType is PasteType.Cut or PasteType.Duplicate && await CompareClipboards())
            return _pasteType;

        return _pasteType = PasteType.Copy;
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
            .Select(file => LocalPathTools.NormalizePath(file.PathToEntry))
            .ToHashSet();
        HashSet<string> directories = _programClipboard
            .Where(entry => entry is DirectoryEntry)
            .Select(directory => LocalPathTools.NormalizePath(directory.PathToEntry))
            .ToHashSet();

        foreach (IStorageItem item in items)
            if (item is IStorageFile)
            {
                if (!files.Contains(LocalPathTools.NormalizePath(item.Path.LocalPath)))
                    return false;
            }
            else if (item is IStorageFolder)
            {
                if (!directories.Contains(LocalPathTools.NormalizePath(item.Path.LocalPath)))
                    return false;
            }

        return true;
    }

    public IFileSystemEntry[] GetClipboard() => _programClipboard.ToArray();

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

    public async Task<IResult> CutAsync(IFileSystemEntry[] selectedFiles, string currentDir)
    {
        SimpleResult result = await CopyToOsClipboardAsync(selectedFiles);
        if (result.IsOk)
        {
            _copyFromDir = currentDir;
            _pasteType = PasteType.Cut;
            _programClipboard = selectedFiles;
        }
        else
            _programClipboard = Array.Empty<IFileSystemEntry>();

        return result;
    }

    public async Task<IResult> CopyAsync(IFileSystemEntry[] selectedFiles, string currentDir)
    {
        SimpleResult result = await CopyToOsClipboardAsync(selectedFiles);
        if (result.IsOk)
        {
            _copyFromDir = currentDir;
            _pasteType = PasteType.Copy;
            _programClipboard = selectedFiles;
        }
        else
            _programClipboard = Array.Empty<IFileSystemEntry>();

        return result;
    }

    public async Task<IResult> CopyPathToFileAsync(string filePath)
    {
        await _systemClipboard.SetTextAsync(filePath);
        return SimpleResult.Ok();
    }

    private ValueResult<IFileSystemEntry> SaveImageToPath(string destinationPath, Bitmap image)
    {
        string imgName = FileNameGenerator.GetAvailableName(
            _fileInfoProvider,
            destinationPath,
            FileSurferSettings.NewImageName + ".png"
        );
        try
        {
            string imagePath = Path.Combine(destinationPath, imgName);
            image.Save(imagePath);
            IFileSystemEntry entry = new FileEntry(imagePath);
            return entry.OkResult();
        }
        catch (Exception ex)
        {
            return ValueResult<IFileSystemEntry>.Error(ex.Message);
        }
        finally
        {
            image.Dispose();
        }
    }

    public async Task<ValueResult<IFileSystemEntry>> PasteImageAsync(string currentDir) =>
        await _systemClipboard.TryGetBitmapAsync() is Bitmap image
            ? SaveImageToPath(currentDir, image)
            : ValueResult<IFileSystemEntry>.Error("No image found in the system clipboard.");

    private async Task<ValueResult<IFileSystemEntry[]>> PasteFromOsClipboard(
        string destinationPath,
        PasteType pasteType
    )
    {
        if (
            await _systemClipboard.TryGetFilesAsync() is not IStorageItem[] items
            || items.Length == 0
        )
            return ValueResult<IFileSystemEntry[]>.Error("No files found in the system clipboard.");

        IFileSystemEntry[] entries = new IFileSystemEntry[items.Length];
        Result result = Result.Ok();

        int index = 0;
        foreach (IStorageItem item in items)
        {
            string normalizedPath = LocalPathTools.NormalizePath(item.Path.LocalPath);
            if (item is IStorageFolder)
            {
                entries[index++] = new DirectoryEntry(normalizedPath);
                result.MergeResult(
                    pasteType is PasteType.Cut
                        ? _fileIoHandler.MoveDirTo(normalizedPath, destinationPath)
                        : _fileIoHandler.CopyDirTo(normalizedPath, destinationPath)
                );
            }
            else if (item is IStorageFile)
            {
                entries[index++] = new FileEntry(normalizedPath);
                result.MergeResult(
                    _pasteType is PasteType.Cut
                        ? _fileIoHandler.MoveFileTo(normalizedPath, destinationPath)
                        : _fileIoHandler.CopyFileTo(normalizedPath, destinationPath)
                );
            }
        }
        return result.IsOk ? entries.OkResult() : ValueResult<IFileSystemEntry[]>.Error(result);
    }

    public async Task<ValueResult<IFileSystemEntry[]>> PasteAsync(
        string currentDir,
        PasteType pasteType
    )
    {
        ValueResult<IFileSystemEntry[]> result = await PasteFromOsClipboard(currentDir, pasteType);
        if (pasteType is PasteType.Cut)
        {
            _programClipboard = Array.Empty<IFileSystemEntry>();
            await _systemClipboard.ClearAsync();
        }
        return result;
    }

    public Task<ValueResult<string[]>> DuplicateAsync(string currentDir)
    {
        if (_programClipboard.Length == 0)
            return Task.FromResult(ValueResult<string[]>.Error("Clipboard is empty"));

        string[] copyNames = new string[_programClipboard.Length];

        Result result = Result.Ok();
        for (int i = 0; i < _programClipboard.Length; i++)
        {
            IFileSystemEntry entry = _programClipboard[i];
            copyNames[i] = FileNameGenerator.GetCopyName(_fileInfoProvider, currentDir, entry);

            result.MergeResult(
                entry is DirectoryEntry
                    ? _fileIoHandler.DuplicateDir(entry.PathToEntry, copyNames[i])
                    : _fileIoHandler.DuplicateFile(entry.PathToEntry, copyNames[i])
            );
        }
        ValueResult<string[]> endResult = result.IsOk
            ? copyNames.OkResult()
            : ValueResult<string[]>.Error(result);

        return Task.FromResult(endResult);
    }
}
