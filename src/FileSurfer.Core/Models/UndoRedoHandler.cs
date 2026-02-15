using System;

namespace FileSurfer.Core.Models;

/// <summary>
/// Generic class for browsing <see cref="FileSurfer"/>'s history, such as file operations and visited directories.
/// </summary>
internal sealed class UndoRedoHandler<T>
{
    private readonly Action? _onCollectionChanged;

    /// <summary>
    /// Nested class representing a node in the <see cref="UndoRedoHandler{T}"/> chain.
    /// </summary>
    private sealed class UndoRedoNode
    {
        internal readonly T? Data;
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

    /// <summary>
    /// Returns the data of the current <see cref="UndoRedoNode"/>.
    /// </summary>
    public T? Current => _current.Data;

    /// <summary>
    /// Constructs a new <see cref="UndoRedoHandler{T}"/> chain.
    /// </summary>
    public UndoRedoHandler(Action? onCollectionChanged = null)
    {
        _onCollectionChanged = onCollectionChanged;
        _head = new UndoRedoNode(default);
        _tail = new UndoRedoNode(default, _head);
        _current = _head;
    }

    /// <summary>
    /// Adds a new node at the current position in the chain and cuts of following nodes.
    /// </summary>
    public void AddNewNode(T data)
    {
        if (_current == _tail)
            MoveToPrevious();

        _current = new UndoRedoNode(data, _current, _tail);
        _onCollectionChanged?.Invoke();
    }

    /// <summary>
    /// Determines if the current <see cref="UndoRedoNode"/> is the end of the <see cref="UndoRedoHandler{T}"/> chain.
    /// </summary>
    public bool IsTail() => _current == _tail;

    /// <summary>
    /// Determines if the current <see cref="UndoRedoNode"/> is the beginning of the <see cref="UndoRedoHandler{T}"/> chain.
    /// </summary>
    public bool IsHead() => _current == _head;

    /// <summary>
    /// Gets the data of the previous <see cref="UndoRedoNode"/> in the <see cref="UndoRedoHandler{T}"/> chain.
    /// </summary>
    public T? GetPrevious() => _current.Previous is null ? default : _current.Previous.Data;

    /// <summary>
    /// Gets the data of the next <see cref="UndoRedoNode"/> in the <see cref="UndoRedoHandler{T}"/> chain.
    /// </summary>
    public T? GetNext() => _current.Next is null ? default : _current.Next.Data;

    /// <summary>
    /// Moves to the previous <see cref="UndoRedoNode"/> in the <see cref="UndoRedoHandler{T}"/> chain.
    /// </summary>
    public void MoveToPrevious()
    {
        if (_current.Previous is null)
            return;

        _current = _current.Previous;
        _onCollectionChanged?.Invoke();
    }

    /// <summary>
    /// Moves to the next <see cref="UndoRedoNode"/> in the <see cref="UndoRedoHandler{T}"/> chain.
    /// </summary>
    public void MoveToNext()
    {
        if (_current.Next is null)
            return;

        _current = _current.Next;
        _onCollectionChanged?.Invoke();
    }

    /// <summary>
    /// Removes the current <see cref="UndoRedoNode"/> from the <see cref="UndoRedoHandler{T}"/> chain.
    /// <para>
    /// Throws <see cref="InvalidOperationException"/> if <see cref="_current"/> is either <see cref="_head"/> or <see cref="_tail"/>.
    /// </para>
    /// </summary>
    /// <param name="goToPrevious"></param>
    /// <exception cref="InvalidOperationException">Throws exception if <see cref="_current"/> is either <see cref="_head"/> or <see cref="_tail"/>.</exception>
    public void RemoveNode(bool goToPrevious)
    {
        if (
            _current.Previous is null
            || _current.Next is null
            || _current == _head
            || _current == _tail
        )
            throw new InvalidOperationException(nameof(_current));

        _current.Previous.Next = _current.Next;
        _current.Next.Previous = _current.Previous;
        _current = goToPrevious ? _current.Previous : _current.Next;
        _onCollectionChanged?.Invoke();
    }
}
