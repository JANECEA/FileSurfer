namespace FileSurfer.Models;

public interface IUndoableFileOperation
{
    public bool Undo(out string? errorMessage);

    public bool Redo(out string? errorMessage);
}
