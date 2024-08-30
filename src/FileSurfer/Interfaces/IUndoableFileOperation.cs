namespace FileSurfer.Models.UndoableFileOperations;

/// <summary>
/// Represents an undoable file operation in the context of the <see cref="FileSurfer"/> app.
/// </summary>
public interface IUndoableFileOperation
{
    /// <summary>
    /// Undoes the file operation.
    /// Implementations of this method should reverse the effects of the original operation.
    /// </summary>
    /// <param name="errorMessage">An output parameter that will contain an error message if the undo operation fails.</param>
    /// <returns><see langword="true"/> if the undo operation was successful, otherwise <see langword="false"/>.</returns> 
    public bool Undo(out string? errorMessage);

    /// <summary>
    /// Redoes the file operation.
    /// Implementations of this method should reapply the effects of the original operation.
    /// </summary>
    /// <param name="errorMessage">An output parameter that will contain an error message if the undo operation fails.</param>
    /// <returns><see langword="true"/> if the redo operation was successful, otherwise <see langword="false"/>.</returns> 
    public bool Redo(out string? errorMessage);
}
