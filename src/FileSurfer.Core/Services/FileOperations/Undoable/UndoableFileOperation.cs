using System.Threading;
using System.Threading.Tasks;
using FileSurfer.Core.Models;
using FileSurfer.Core.Services.Dialogs;

namespace FileSurfer.Core.Services.FileOperations.Undoable;

/// <summary>
/// Represents an undoable file operation in the context of the <see cref="FileSurfer"/> app.
/// </summary>
public interface IUndoableFileOperation : IFileOperation
{
    /// <summary>
    /// Undoes the file operation.
    /// Implementations of this method should reverse the effects of the original operation.
    /// </summary>
    /// <param name="reporter">
    /// Progress reporter used to publish undo progress updates.
    /// </param>
    /// <param name="ct">
    /// Cancellation token used to stop the undo operation.
    /// </param>
    /// <returns>
    /// A task that returns the undo result, including any error details if rollback fails.
    /// </returns>
    public Task<IResult> UndoAsync(ProgressReporter reporter, CancellationToken ct);
}

/// <summary>
/// Extends <see cref="FileOperation"/> with an undo pipeline that replays inverse actions
/// for each processed entry.
/// </summary>
public abstract class UndoableFileOperation : FileOperation, IUndoableFileOperation
{
    /// <summary>
    /// Gets the operation verb used while reporting undo progress.
    /// </summary>
    protected abstract string UndoVerb { get; }

    /// <summary>
    /// Initializes an undoable file operation with the handler and entries to process.
    /// </summary>
    /// <param name="fileIoHandler">
    /// File I/O handler used by derived operations for invoke and undo actions.
    /// </param>
    /// <param name="entries">
    /// Entries targeted by this operation.
    /// </param>
    protected UndoableFileOperation(IFileIoHandler fileIoHandler, IFileSystemEntry[] entries)
        : base(fileIoHandler, entries) { }

    public Task<IResult> UndoAsync(ProgressReporter reporter, CancellationToken ct) =>
        Task.Run(() => InvokeInternal(UndoAction, UndoVerb, reporter, ct), ct);

    /// <summary>
    /// Represents an undo-action invoked on <see cref="IFileSystemEntry"/> from <see cref="FileOperation.Entries"/>.
    /// </summary>
    /// <param name="entry">
    /// The entry currently being rolled back.
    /// </param>
    /// <param name="index">
    /// Zero-based index of <paramref name="entry"/> in <see cref="FileOperation.Entries"/>.
    /// </param>
    /// <returns>
    /// The operation result for the undo action applied to the current entry.
    /// </returns>
    protected abstract IResult UndoAction(IFileSystemEntry entry, int index);
}
