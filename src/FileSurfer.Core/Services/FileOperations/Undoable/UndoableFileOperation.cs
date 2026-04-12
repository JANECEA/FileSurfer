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
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public Task<IResult> UndoAsync(ProgressReporter reporter, CancellationToken ct);
}

public abstract class UndoableFileOperation : FileOperation, IUndoableFileOperation
{
    protected abstract string UndoVerb { get; }

    protected UndoableFileOperation(IFileIoHandler fileIoHandler, IFileSystemEntry[] entries)
        : base(fileIoHandler, entries) { }

    public Task<IResult> UndoAsync(ProgressReporter reporter, CancellationToken ct) =>
        Task.Run(() => InvokeInternal(UndoAction, UndoVerb, reporter, ct), ct);

    /// <summary>
    /// Represents an undo-action invoked on <see cref="IFileSystemEntry"/> from <see cref="FileOperation.Entries"/>.
    /// </summary>
    /// <param name="entry"><see cref="IFileSystemEntry"/> for the undo-action.</param>
    /// <param name="index">Entry's index in <see cref="FileOperation.Entries"/>, in case it is useful.</param>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    protected abstract IResult UndoAction(IFileSystemEntry entry, int index);
}
