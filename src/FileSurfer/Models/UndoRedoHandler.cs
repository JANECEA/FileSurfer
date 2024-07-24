using System;

namespace FileSurfer;

class UndoRedoHandler
{
    private UndoRedoNode? _current = null;

    class UndoRedoNode
    {
        internal UndoRedoNode? Previous;
        internal IUndoableFileOperation Operation;
        internal UndoRedoNode? Next;
        internal bool EndOfChain = false;

        internal UndoRedoNode(
            IUndoableFileOperation operation,
            UndoRedoNode? previous = null
        )
        {
            Operation = operation;
            Previous = previous;

            if (Previous is not null)
            {
                Previous.Next = this;
                Previous.EndOfChain = false;
            }
        }
    }

    public void NewOperation(IUndoableFileOperation operation) =>
        _current = new UndoRedoNode(operation, _current);

    public bool Undo(out string? errorMessage)
    {
        if (_current is null || (_current.EndOfChain && _current.Previous is null))
        {
            errorMessage = "Nothing left to undo";
            return false;
        }
        if (!_current.Operation.Undo(out errorMessage))
        {
            RemoveNode(true);
            return false;
        }

        if (_current.Previous is null)
            _current.EndOfChain = true;
        else
            _current = _current.Previous;
        return true;
    }

    public bool Redo(out string? errorMessage)
    {
        if (_current is null || (_current.EndOfChain && _current.Next is null))
        {
            errorMessage = "Nothing left to redo";
            return false;
        }
        if (!_current.Operation.Redo(out errorMessage))
        {
            RemoveNode(false);
            return false;
        }

        if (_current.Next is null)
            _current.EndOfChain = true;
        else
            _current = _current.Next;
        return true;
    }

    private void RemoveNode(bool undo)
    {
        if (_current is null)
            throw new ArgumentNullException();

        if (_current.Previous is null || _current.Next is null)
        {
            RemoveEndNode();
            return;
        }
        _current.Previous.Next = _current.Next;
        _current.Next.Previous = _current.Previous;
        _current = undo ? _current.Previous : _current.Next;
    }

    private void RemoveEndNode()
    {
        if (_current is null)
            throw new ArgumentNullException();

        if (_current.Previous is null  && _current.Next is not null)
        {
            _current.Next.Previous = _current.Previous;
            _current.Next.EndOfChain = true;
            _current = _current.Next;
            return;
        }
        if (_current.Previous is not null && _current.Next is null)
        {
            _current.Previous.Next = _current.Next;
            _current.Previous.EndOfChain = true;
            _current = _current.Previous;
            return;
        }
        _current = null;
    }
}

