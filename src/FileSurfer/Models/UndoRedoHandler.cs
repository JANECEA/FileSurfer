using System;

namespace FileSurfer;

class UndoRedoHandler<T>
{
    class UndoRedoNode
    {
        internal T? Data;
        internal UndoRedoNode? Previous;
        internal UndoRedoNode? Next;

        internal UndoRedoNode(T? data, UndoRedoNode? previous = null, UndoRedoNode? next = null)
        {
            Data = data;
            Previous = previous;
            Next = next;

            if (Previous is not null)
                Previous.Next = this;

            if (Next is not null)
                Next.Previous = this;
        }
    }

    private readonly UndoRedoNode _head;
    private readonly UndoRedoNode _tail;
    private UndoRedoNode _current;
    public T? Current => _current.Data;

    public UndoRedoHandler()
    {
        _head = new(default);
        _tail = new(default, _head);
        _current = _head;
    }

    public void NewNode(T data)
    {
        if (_current == _tail)
            MoveToPrevious();

        _current = new UndoRedoNode(data, _current, _tail);
    }

    public bool IsTail() => _current == _tail;

    public bool IsHead() => _current == _head;

    public T? GetPrevious() => _current.Previous is null ? default : _current.Previous.Data;

    public T? GetNext() => _current.Next is null ? default : _current.Next.Data;

    public void MoveToPrevious() => _current = _current.Previous ?? _current;

    public void MoveToNext()  => _current = _current.Next ?? _current;

    public void RemoveNode(bool goToPrevious)
    {
        if (
            _current.Previous is null
            || _current.Next is null
            || _current == _head
            || _current == _tail
        )
        {
            throw new DataMisalignedException(nameof(_current));
        }
        _current.Previous.Next = _current.Next;
        _current.Next.Previous = _current.Previous;
        _current = goToPrevious ? _current.Previous : _current.Next;
    }
}
