using FileSurfer.Core.Models;

namespace FileSurfer.Core.Services.FileOperations;

public sealed class DeleteFiles : FileOperation
{
    protected override string InvokeVerb => "Deleting";

    public DeleteFiles(IFileIoHandler fileIoHandler, IFileSystemEntry[] entries)
        : base(fileIoHandler, entries) { }

    protected override IResult InvokeAction(IFileSystemEntry entry, int index) =>
        entry is DirectoryEntry
            ? FileIoHandler.DeleteDir(entry.PathToEntry)
            : FileIoHandler.DeleteFile(entry.PathToEntry);
}
