using System.Threading;
using System.Threading.Tasks;
using FileSurfer.Core.Models;
using FileSurfer.Core.Services.Dialogs;

namespace FileSurfer.Core.Services.FileOperations.Undoable;

/// <summary>
/// Represents an undoable file operation in the context of the <see cref="FileSurfer"/> app.
/// </summary>
public interface IUndoableFileOperation
{
    /// <summary>
    /// Invokes the file operation.
    /// Implementations of this method should apply the effects of the operation.
    /// </summary>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public Task<IResult> InvokeAsync(ProgressReporter reporter, CancellationToken ct);

    /// <summary>
    /// Undoes the file operation.
    /// Implementations of this method should reverse the effects of the original operation.
    /// </summary>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public Task<IResult> UndoAsync(ProgressReporter reporter, CancellationToken ct);
}
