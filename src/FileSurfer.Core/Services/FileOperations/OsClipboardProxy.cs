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
internal class OsClipboardProxy
{
    private static readonly DataTransferItem ClearedMarkItem = DataTransferItem.Create(
        DataFormat.CreateBytesApplicationFormat("filesurfer-clipboard-empty"),
        new byte[] { 1, 2, 3 }
    );

    private readonly IStorageProvider _storageProvider;

    private IClipboard Clipboard { get; }

    /// <summary>
    /// Initializes a clipboard proxy with the platform clipboard and storage provider abstractions.
    /// </summary>
    /// <param name="clipboard">
    /// The OS clipboard abstraction used for read/write clipboard operations.
    /// </param>
    /// <param name="storageProvider">
    /// The storage provider used to resolve application paths into OS storage items.
    /// </param>
    internal OsClipboardProxy(IClipboard clipboard, IStorageProvider storageProvider)
    {
        Clipboard = clipboard;
        _storageProvider = storageProvider;
    }

    /// <summary>
    /// Executes an asynchronous clipboard operation on the UI thread and returns its result.
    /// </summary>
    /// <typeparam name="T">
    /// The result type produced by the provided operation.
    /// </typeparam>
    /// <param name="operation">
    /// The asynchronous clipboard operation to run against the current clipboard instance.
    /// </param>
    /// <returns>
    /// A task that resolves to the value returned by <paramref name="operation"/>.
    /// </returns>
    internal Task<T> ExecuteAsync<T>(Func<IClipboard, Task<T>> operation) =>
        Dispatcher.UIThread.InvokeAsync(() => operation(Clipboard));

    /// <summary>
    /// Executes an asynchronous clipboard operation on the UI thread.
    /// </summary>
    /// <param name="operation">
    /// The asynchronous clipboard operation to run against the current clipboard instance.
    /// </param>
    /// <returns>
    /// A task that completes when the provided operation has finished.
    /// </returns>
    internal Task ExecuteAsync(Func<IClipboard, Task> operation) =>
        Dispatcher.UIThread.InvokeAsync(() => operation(Clipboard));

    /// <summary>
    /// Asynchronously clears the OS clipboard on the UI thread by setting a marker object.
    /// </summary>
    /// <returns>
    /// A task that completes when the clearing operation has finished.
    /// </returns>
    internal Task ClearAsync() =>
        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            DataTransfer data = new();
            data.Add(ClearedMarkItem);
            await Clipboard.SetDataAsync(data);
        });

    /// <summary>
    /// Compares OS clipboard storage items with FileSurfer clipboard entries by normalized paths and item type.
    /// </summary>
    /// <param name="osItems">
    /// The storage items currently present in the OS clipboard.
    /// </param>
    /// <param name="programItems">
    /// The entries tracked by FileSurfer's internal clipboard state.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when both collections represent the same files/directories; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    internal static bool CompareClipboards(
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

    /// <summary>
    /// Copies FileSurfer entries to the OS clipboard as storage items.
    /// </summary>
    /// <param name="entries">
    /// The file-system entries to place on the OS clipboard.
    /// </param>
    /// <returns>
    /// A task that returns the operation result, including error details when clipboard population fails.
    /// </returns>
    internal Task<IResult> CopyToOsClipboardAsync(IFileSystemEntry[] entries) =>
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
