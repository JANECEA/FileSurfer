using FileSurfer.Core.Models;

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
    public IResult Invoke();

    /// <summary>
    /// Undoes the file operation.
    /// Implementations of this method should reverse the effects of the original operation.
    /// </summary>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public IResult Undo();
}
