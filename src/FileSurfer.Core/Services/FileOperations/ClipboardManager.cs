using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using FileSurfer.Core.Extensions;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileInformation;
using FileSurfer.Core.Services.Dialogs;
using FileSurfer.Core.Services.FileOperations.Undoable;

namespace FileSurfer.Core.Services.FileOperations;

using OpResult = ValueResult<IUndoableFileOperation?>;

/// <summary>
/// Interacts with the program and system clipboards using <see cref="Avalonia.Input.Platform.IClipboard"/>.
/// </summary>
public class ClipboardManager : IClipboardManager
{
    private enum PasteType
    {
        Copy,
        Cut,
    }

    private const string ImageExtension = ".png";
    private const string TextExtension = ".txt";
    private static readonly OpResult CutSameDirectoryResult = OpResult.Error(
        "Cannot move files to the same directory."
    );

    private readonly OsClipboardFacade _osClipboard;
    private readonly LocalFileSystem _localFs;

    private Location? _origin;
    private readonly List<IFileSystemEntry> _programClipboard = [];
    private PasteType _pasteType = PasteType.Copy;

    public ClipboardManager(
        IClipboard clipboard,
        IStorageProvider storageProvider,
        LocalFileSystem localFs
    )
    {
        _osClipboard = new OsClipboardFacade(clipboard, storageProvider);
        _localFs = localFs;
    }

    public async Task<IResult> CopyPathToFileAsync(string filePath)
    {
        await _osClipboard.Clipboard.SetTextAsync(filePath);
        return SimpleResult.Ok();
    }

    private async Task<IResult> SetClipboardInternal(
        IFileSystemEntry[] entries,
        Location location,
        PasteType pasteType
    )
    {
        if (location.FileSystem.IsLocal())
        {
            IResult result = await _osClipboard.CopyToOsClipboardAsync(entries);
            if (!result.IsOk)
            {
                _programClipboard.Clear();
                _origin = null;
                return result;
            }
        }
        else
            await _osClipboard.Clipboard.ClearAsync();

        _pasteType = pasteType;
        _origin = location;
        _programClipboard.Clear();
        _programClipboard.AddRange(entries);

        return SimpleResult.Ok();
    }

    public async Task<IResult> CutAsync(
        IFileSystemEntry[] selectedFiles,
        Location currentLocation
    ) => await SetClipboardInternal(selectedFiles, currentLocation, PasteType.Cut);

    public async Task<IResult> CopyAsync(
        IFileSystemEntry[] selectedFiles,
        Location currentLocation
    ) => await SetClipboardInternal(selectedFiles, currentLocation, PasteType.Copy);

    private static async Task<OpResult> SaveImageToPath(Location destination, Bitmap bitmap)
    {
        string imgName = FileNameGenerator.GetAvailableName(
            destination.FileSystem.FileInfoProvider,
            destination.Path,
            FileSurferSettings.NewImageName + ImageExtension
        );
        try
        {
            MemoryStream stream = new();
            bitmap.Save(stream);
            stream.Position = 0;

            using FileTransferStream fileStream = new(imgName, stream);
            IResult result = await destination.FileSystem.FileIoHandler.WriteFileStream(
                fileStream,
                destination.Path,
                new ProgressReporter(),
                CancellationToken.None
            );
            return result.IsOk ? OpResult.Ok(null) : OpResult.Error(result);
        }
        catch (Exception ex)
        {
            return OpResult.Error(ex.Message);
        }
        finally
        {
            bitmap.Dispose();
        }
    }

    private static async Task<OpResult> SaveTextToPath(Location destination, string text)
    {
        string textName = FileNameGenerator.GetAvailableName(
            destination.FileSystem.FileInfoProvider,
            destination.Path,
            FileSurferSettings.NewTextFileName + TextExtension
        );
        try
        {
            MemoryStream stream = new(Encoding.UTF8.GetBytes(text));
            using FileTransferStream fileStream = new(textName, stream);
            IResult result = await destination.FileSystem.FileIoHandler.WriteFileStream(
                fileStream,
                destination.Path,
                new ProgressReporter(),
                CancellationToken.None
            );
            return result.IsOk ? OpResult.Ok(null) : OpResult.Error(result);
        }
        catch (Exception ex)
        {
            return OpResult.Error(ex.Message);
        }
    }

    private Location DetermineBaseLocation()
    {
        if (_programClipboard.Count == 0)
            throw new UnreachableException();

        if (_programClipboard.Count == 1)
            return new Location(
                _localFs,
                LocalPathTools.GetParentDir(_programClipboard[0].PathToEntry)
            );

        int minLength = _programClipboard.Min(entry => entry.PathToEntry.Length);

        int i = 0;
        for (; i < minLength; i++)
        {
            char ch = _programClipboard[0].PathToEntry[i];

            if (_programClipboard.Any(entry => entry.PathToEntry[i] != ch))
                break;
        }
        return new Location(_localFs, _programClipboard[0].PathToEntry[..i]);
    }

    private void PlaceInProgramClipboard(IStorageItem[] items)
    {
        _programClipboard.Clear();
        foreach (IStorageItem item in items)
            if (item is IStorageFolder)
                _programClipboard.Add(
                    new DirectoryEntry(
                        LocalPathTools.NormalizePath(item.Path.LocalPath),
                        LocalPathTools.Instance
                    )
                );
            else if (item is IStorageFile)
                _programClipboard.Add(
                    new FileEntry(
                        LocalPathTools.NormalizePath(item.Path.LocalPath),
                        LocalPathTools.Instance
                    )
                );
    }

    public async Task<OpResult> PasteAsync(
        Location destination,
        ProgressReporter reporter,
        CancellationToken ct
    )
    {
        if (await _osClipboard.Clipboard.TryGetBitmapAsync() is Bitmap bitmap)
            return await SaveImageToPath(destination, bitmap);

        if (await _osClipboard.Clipboard.TryGetTextAsync() is string text)
            return await SaveTextToPath(destination, text);

        if (
            await _osClipboard.Clipboard.TryGetFilesAsync() is IStorageItem[] items
            && !OsClipboardFacade.CompareClipboards(items, _programClipboard)
        )
        {
            _pasteType = PasteType.Copy;
            PlaceInProgramClipboard(items);
            _origin = DetermineBaseLocation();
        }

        if (_origin is null || _programClipboard.Count == 0)
            return OpResult.Error("Clipboard is empty.");

        return await PasteInternal(destination);
    }

    private async Task<OpResult> PasteInternal(Location destination)
    {
        bool destIsSame = destination.IsSame(_origin);
        bool fsIsSame = destination.FileSystem.IsSame(_origin?.FileSystem);

        OpResult result = _pasteType switch
        {
            PasteType.Copy when destIsSame => DuplicateSameFs(destination),
            PasteType.Copy when fsIsSame => CopySameFs(destination),
            PasteType.Copy => await UploadFiles(destination),
            PasteType.Cut when destIsSame => CutSameDirectoryResult,
            PasteType.Cut when fsIsSame => MoveSameFs(destination),
            PasteType.Cut => await UploadAndDelete(destination),
            _ => throw new UnreachableException(),
        };

        if (result.IsOk && _pasteType is PasteType.Cut)
        {
            await _osClipboard.Clipboard.ClearAsync();
            _programClipboard.Clear();
            _origin = null;
        }
        return result;
    }

    private static ValueResult<FileTransferStream> ToFileTransferStream(
        IFileSystemEntry e,
        Location l
    ) => FileTransferStream.FromInfoProvider(l.FileSystem.FileInfoProvider, e.PathToEntry);

    private static ValueResult<DirTransferStream> ToDirTransferStream(
        IFileSystemEntry e,
        Location l,
        bool includeHidden,
        bool includeOs
    ) =>
        DirTransferStream.FromInfoProvider(
            l.FileSystem.FileInfoProvider,
            e.PathToEntry,
            includeHidden,
            includeOs
        );

    private ValueResult<(FileTransferStream[], DirTransferStream[])> GetStreams()
    {
        bool includeHidden = FileSurferSettings.ShowHiddenFiles;
        bool includeOs = FileSurferSettings.ShowProtectedFiles;

        List<ValueResult<FileTransferStream>> fileResults = _programClipboard
            .Where(e => e is FileEntry)
            .Select(e => ToFileTransferStream(e, _origin!))
            .ToList();

        List<ValueResult<DirTransferStream>> dirResults = _programClipboard
            .Where(e => e is DirectoryEntry)
            .Select(e => ToDirTransferStream(e, _origin!, includeHidden, includeOs))
            .ToList();

        IResult? errResult = fileResults
            .Cast<IResult>()
            .Concat(dirResults)
            .FirstOrDefault(r => !r.IsOk);

        if (errResult is not null)
        {
            foreach (ValueResult<FileTransferStream> r in fileResults.Where(r => r.IsOk))
                r.Value.Dispose();
            foreach (ValueResult<DirTransferStream> r in dirResults.Where(r => r.IsOk))
                r.Value.Dispose();

            return ValueResult<(FileTransferStream[], DirTransferStream[])>.Error(errResult);
        }
        return (
            fileResults.Select(r => r.Value).ToArray(),
            dirResults.Select(r => r.Value).ToArray()
        ).OkResult();
    }

    private async Task<OpResult> UploadAndDelete(Location destination)
    {
        OpResult result = await UploadFiles(destination);
        if (!result.IsOk)
            return result;

        IFileIoHandler f = _origin!.FileSystem.FileIoHandler;
        foreach (IFileSystemEntry e in _programClipboard)
        {
            IResult r = e is FileEntry ? f.DeleteFile(e.PathToEntry) : f.DeleteDir(e.PathToEntry);
            if (!r.IsOk)
                return OpResult.Error(r);
        }

        return OpResult.Ok(null);
    }

    private async Task<OpResult> UploadFiles(Location destination)
    {
        var streamsR = GetStreams();
        if (!streamsR.IsOk)
            return OpResult.Error(streamsR);

        (FileTransferStream[] files, DirTransferStream[] dirs) = streamsR.Value;

        IResult result = await UploadAll(files, dirs, destination);
        foreach (IDisposable disposable in files.Cast<IDisposable>().Concat(dirs))
            disposable.Dispose();

        return result.IsOk ? OpResult.Ok(null) : OpResult.Error(result);
    }

    private static async Task<IResult> UploadAll(
        FileTransferStream[] files,
        DirTransferStream[] dirs,
        Location destination
    )
    {
        IFileIoHandler f = destination.FileSystem.FileIoHandler;

        foreach (FileTransferStream file in files)
        {
            IResult r = await f.WriteFileStream(
                file,
                destination.Path,
                new ProgressReporter(),
                CancellationToken.None
            );
            if (!r.IsOk)
                return r;
        }
        foreach (DirTransferStream dir in dirs)
        {
            IResult r = await f.WriteDirStream(
                dir,
                destination.Path,
                new ProgressReporter(),
                CancellationToken.None
            );
            if (!r.IsOk)
                return r;
        }

        return SimpleResult.Ok();
    }

    private OpResult CopySameFs(Location destination)
    {
        CopyFilesTo op = new(
            destination.FileSystem.FileInfoProvider.PathTools,
            destination.FileSystem.FileIoHandler,
            _programClipboard.ToArray(),
            destination.Path
        );

        IResult result = op.Invoke();
        return result.IsOk ? OpResult.Ok(op) : OpResult.Error(result);
    }

    private OpResult MoveSameFs(Location destination)
    {
        MoveFilesTo op = new(
            destination.FileSystem.FileInfoProvider.PathTools,
            destination.FileSystem.FileIoHandler,
            _programClipboard.ToArray(),
            destination.Path
        );

        IResult result = op.Invoke();
        return result.IsOk ? OpResult.Ok(op) : OpResult.Error(result);
    }

    private OpResult DuplicateSameFs(Location currentLocation)
    {
        IFileInfoProvider f = currentLocation.FileSystem.FileInfoProvider;

        string[] copyNames = _programClipboard
            .Select(entry => FileNameGenerator.GetCopyName(f, currentLocation.Path, entry))
            .ToArray();

        DuplicateFiles op = new(
            f.PathTools,
            currentLocation.FileSystem.FileIoHandler,
            _programClipboard.ToArray(),
            copyNames
        );

        IResult result = op.Invoke();
        return result.IsOk ? OpResult.Ok(op) : OpResult.Error(result);
    }
}
