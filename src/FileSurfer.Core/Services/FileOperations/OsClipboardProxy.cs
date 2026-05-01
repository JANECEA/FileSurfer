using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
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

    private readonly IClipboard _clipboard;
    private readonly IStorageProvider _storageProvider;

    public OsClipboardProxy(IClipboard clipboard, IStorageProvider storageProvider)
    {
        _clipboard = clipboard;
        _storageProvider = storageProvider;
    }

    public Task<IResult> SetTextAsync(string text) =>
        Dispatcher.UIThread.InvokeAsync<IResult>(async () =>
        {
            try
            {
                await _clipboard.SetTextAsync(text);
                return SimpleResult.Ok();
            }
            catch (Exception ex)
            {
                return SimpleResult.Error(ex.Message);
            }
        });

    public Task<Bitmap?> TryGetBitmapAsync() =>
        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            try
            {
                return await _clipboard.TryGetBitmapAsync();
            }
            catch
            {
                return null;
            }
        });

    public Task<string?> TryGetTextAsync() =>
        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            try
            {
                return await _clipboard.TryGetTextAsync();
            }
            catch
            {
                return null;
            }
        });

    private IFileSystemEntry? FromStorageItem(IStorageItem item) =>
        item switch
        {
            IStorageFolder folder => new DirectoryEntry(
                LocalPathTools.NormalizePath(folder.Path.LocalPath),
                LocalPathTools.Instance
            ),
            IStorageFile file => new FileEntry(
                LocalPathTools.NormalizePath(file.Path.LocalPath),
                LocalPathTools.Instance
            ),
            _ => null,
        };

    public Task<IFileSystemEntry[]?> TryGetFilesAsync() =>
        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            try
            {
                IStorageItem[]? files = await _clipboard.TryGetFilesAsync();
                return files?.Select(FromStorageItem).OfType<IFileSystemEntry>().ToArray();
            }
            catch
            {
                return null;
            }
        });

    public Task<IResult> ClearAsync() =>
        Dispatcher.UIThread.InvokeAsync<IResult>(async () =>
        {
            try
            {
                DataTransfer data = new();
                data.Add(ClearedMarkItem);
                await _clipboard.SetDataAsync(data);
                return SimpleResult.Ok();
            }
            catch (Exception ex)
            {
                return SimpleResult.Error(ex.Message);
            }
        });

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
            await _clipboard.SetFilesAsync(storageItems);
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            _ = await ClearAsync();
            return SimpleResult.Error(ex.Message);
        }
    }
}
