using FileSurfer.Core.Models;

namespace FileSurfer.Core.Services.FileOperations;

/// <summary>
/// Deletes a batch of file-system entries using the configured <see cref="IFileIoHandler"/>.
/// </summary>
public sealed class DeleteFiles : FileOperation
{
    protected override string InvokeVerb => "Deleting";

    /// <summary>
    /// Initializes a delete operation for the provided file-system entries.
    /// </summary>
    /// <param name="fileIoHandler">
    /// File I/O handler used to perform file and directory deletion.
    /// </param>
    /// <param name="entries">
    /// The entries to delete.
    /// </param>
    public DeleteFiles(IFileIoHandler fileIoHandler, IFileSystemEntry[] entries)
        : base(fileIoHandler, entries) { }

    protected override IResult InvokeAction(IFileSystemEntry entry, int index) =>
        entry is DirectoryEntry
            ? FileIoHandler.DeleteDir(entry.PathToEntry)
            : FileIoHandler.DeleteFile(entry.PathToEntry);
}
