using System.Threading.Tasks;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileInformation;

namespace FileSurfer.Core.Services.FileOperations;

public class BasicClipboardManager : IClipboardManager
{
    private readonly IFileInfoProvider _fileInfoProvider;
    private readonly IFileIoHandler _fileIoHandler;

    private string _pasteFromDir = string.Empty;
    private PasteType _pasteType = PasteType.Copy;
    private IFileSystemEntry[] _clipboard = [];

    public BasicClipboardManager(IFileInfoProvider fileInfoProvider, IFileIoHandler fileIoHandler)
    {
        _fileInfoProvider = fileInfoProvider;
        _fileIoHandler = fileIoHandler;
    }

    public Task<PasteType> GetOperationType(string currentDir)
    {
        if (_pasteType == PasteType.Cut)
            return Task.FromResult(PasteType.Cut);

        if (_pasteFromDir == currentDir)
            return Task.FromResult(PasteType.Duplicate);

        return Task.FromResult(PasteType.Copy);
    }

    public IFileSystemEntry[] GetClipboard() => _clipboard;

    public Task<IResult> CutAsync(IFileSystemEntry[] selectedFiles, string currentDir)
    {
        _clipboard = selectedFiles;
        _pasteType = PasteType.Cut;
        _pasteFromDir = currentDir;
        return Task.FromResult<IResult>(SimpleResult.Ok());
    }

    public Task<IResult> CopyAsync(IFileSystemEntry[] selectedFiles, string currentDir)
    {
        _clipboard = selectedFiles;
        _pasteType = PasteType.Copy;
        _pasteFromDir = currentDir;
        return Task.FromResult<IResult>(SimpleResult.Ok());
    }

    private ValueResult<IFileSystemEntry[]> PasteFromClipboard(
        string destinationPath,
        PasteType pasteType
    )
    {
        IFileSystemEntry[] entries = new IFileSystemEntry[_clipboard.Length];
        Result result = Result.Ok();
        int index = 0;
        foreach (IFileSystemEntry entry in _clipboard)
            if (entry is DirectoryEntry)
            {
                entries[index++] = new DirectoryEntry(entry.PathToEntry);
                result.MergeResult(
                    pasteType is PasteType.Cut
                        ? _fileIoHandler.MoveDirTo(entry.PathToEntry, destinationPath)
                        : _fileIoHandler.CopyDirTo(entry.PathToEntry, destinationPath)
                );
            }
            else if (entry is FileEntry)
            {
                entries[index++] = new FileEntry(entry.PathToEntry);
                result.MergeResult(
                    _pasteType is PasteType.Cut
                        ? _fileIoHandler.MoveFileTo(entry.PathToEntry, destinationPath)
                        : _fileIoHandler.CopyFileTo(entry.PathToEntry, destinationPath)
                );
            }

        return result.IsOk ? entries.OkResult() : ValueResult<IFileSystemEntry[]>.Error(result);
    }

    public Task<ValueResult<IFileSystemEntry[]>> PasteAsync(string currentDir, PasteType pasteType)
    {
        ValueResult<IFileSystemEntry[]> result = PasteFromClipboard(currentDir, pasteType);
        if (pasteType is PasteType.Cut)
            _clipboard = [];

        return Task.FromResult(result);
    }

    public Task<ValueResult<string[]>> DuplicateAsync(string currentDir)
    {
        if (_clipboard.Length == 0)
            return Task.FromResult(ValueResult<string[]>.Error("Clipboard is empty"));

        string[] copyNames = new string[_clipboard.Length];

        Result result = Result.Ok();
        for (int i = 0; i < _clipboard.Length; i++)
        {
            IFileSystemEntry entry = _clipboard[i];
            copyNames[i] = FileNameGenerator.GetCopyName(_fileInfoProvider, currentDir, entry);

            result.MergeResult(
                entry is DirectoryEntry
                    ? _fileIoHandler.DuplicateDir(entry.PathToEntry, copyNames[i])
                    : _fileIoHandler.DuplicateFile(entry.PathToEntry, copyNames[i])
            );
        }

        return Task.FromResult(
            result.IsOk ? copyNames.OkResult() : ValueResult<string[]>.Error(result)
        );
    }
}
