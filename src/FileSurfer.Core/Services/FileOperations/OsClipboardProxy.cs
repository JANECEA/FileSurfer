using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using FileSurfer.Core.Models;

namespace FileSurfer.Core.Services.FileOperations;

/// <summary>
/// Provides UI-thread-safe clipboard and storage-provider helpers used by
/// <see cref="ClipboardManager"/> when synchronizing FileSurfer selections with the OS clipboard.
/// </summary>
public class OsClipboardProxy : IOsClipboardProxy
{
    private static readonly DataTransferItem ClearedMarkItem = DataTransferItem.Create(
        DataFormat.CreateBytesApplicationFormat("filesurfer-clipboard-empty"),
        new byte[] { 1, 2, 3 }
    );

    private readonly IStorageProvider _storageProvider;

    private IClipboard Clipboard { get; }

    public OsClipboardProxy(IClipboard clipboard, IStorageProvider storageProvider)
    {
        Clipboard = clipboard;
        _storageProvider = storageProvider;
    }

    public Task<T> ExecuteAsync<T>(Func<IClipboard, Task<T>> operation) =>
        Dispatcher.UIThread.InvokeAsync(() => operation(Clipboard));

    public Task ExecuteAsync(Func<IClipboard, Task> operation) =>
        Dispatcher.UIThread.InvokeAsync(() => operation(Clipboard));

    public Task<IResult> ClearAsync() =>
        Dispatcher.UIThread.InvokeAsync<IResult>(async () =>
        {
            try
            {
                DataTransfer data = new();
                data.Add(ClearedMarkItem);
                await Clipboard.SetDataAsync(data);
                return SimpleResult.Ok();
            }
            catch (Exception ex)
            {
                return SimpleResult.Error(ex.Message);
            }
        });

    public static bool CompareClipboards(
        IStorageItem[] osItems,
        IList<IFileSystemEntry> programItems
    )
    {
        if (osItems.Length != programItems.Count)
            return false;

        HashSet<string> files = programItems
            .Where(entry => entry is FileEntry)
            .Select(file => LocalPathTools.NormalizePath(file.PathToEntry))
            .ToHashSet();
        HashSet<string> directories = programItems
            .Where(entry => entry is DirectoryEntry)
            .Select(directory => LocalPathTools.NormalizePath(directory.PathToEntry))
            .ToHashSet();

        foreach (IStorageItem item in osItems)
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

    public Task<IResult> CopyToOsClipboardAsync(IFileSystemEntry[] entries) =>
        Dispatcher.UIThread.InvokeAsync(() => CopyToOsClipboardInternal(entries));

    private async Task<IResult> CopyToOsClipboardInternal(IFileSystemEntry[] entries)
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
            await Clipboard.SetFilesAsync(storageItems);
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            await Clipboard.ClearAsync();
            return SimpleResult.Error(ex.Message);
        }
    }
}
