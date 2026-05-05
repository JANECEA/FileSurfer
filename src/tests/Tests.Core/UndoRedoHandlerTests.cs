using FileSurfer.Core.Models;

namespace Tests.Core;

public class UndoRedoHandlerTests
{
    [Fact]
    public void Constructor_InitializesAtHead_WithEmptyState()
    {
        UndoRedoHandler<string> handler = new();

        Assert.True(handler.IsHead());
        Assert.False(handler.IsTail());
        Assert.Null(handler.Current);
        Assert.Null(handler.GetPrevious());
        Assert.Null(handler.GetNext());
    }

    [Fact]
    public void MoveToNextAndPrevious_RespectsBoundaries_AndCallsCallbackOnChanges()
    {
        int changedCount = 0;
        UndoRedoHandler<string> handler = new(() => changedCount++);

        handler.MoveToPrevious();
        Assert.Equal(0, changedCount);

        handler.MoveToNext();
        Assert.True(handler.IsTail());
        Assert.Equal(1, changedCount);

        handler.MoveToNext();
        Assert.Equal(1, changedCount);

        handler.MoveToPrevious();
        Assert.True(handler.IsHead());
        Assert.Equal(2, changedCount);

        handler.MoveToPrevious();
        Assert.Equal(2, changedCount);
    }

    [Fact]
    public void AddNewNode_DiscardsForwardHistory_WhenAddingFromMiddle()
    {
        UndoRedoHandler<string> handler = new();
        handler.AddNewNode("a");
        handler.AddNewNode("b");
        handler.AddNewNode("c");
        handler.MoveToPrevious();

        Assert.Equal("b", handler.Current);
        Assert.Equal("c", handler.GetNext());

        handler.AddNewNode("d");

        Assert.Equal("d", handler.Current);
        Assert.Equal("b", handler.GetPrevious());
        Assert.Null(handler.GetNext());
        Assert.Empty(handler.EnumerateFromCurrentForward());
    }

    [Fact]
    public void EnumerateFromCurrentBackAndForward_ReturnsExpectedOrder()
    {
        UndoRedoHandler<string> handler = new();
        handler.AddNewNode("a");
        handler.AddNewNode("b");
        handler.AddNewNode("c");
        handler.MoveToPrevious();

        Assert.Equal(["c"], handler.EnumerateFromCurrentForward().ToArray());
        Assert.Equal(["a"], handler.EnumerateFromCurrentBack().ToArray());
    }

    [Fact]
    public void RemoveCurrent_GoToPrevious_RemovesNodeAndMovesBack()
    {
        UndoRedoHandler<string> handler = new();
        handler.AddNewNode("a");
        handler.AddNewNode("b");
        handler.AddNewNode("c");

        handler.RemoveCurrent(goToPrevious: true);

        Assert.Equal("b", handler.Current);
        Assert.Null(handler.GetNext());
        Assert.Equal("a", handler.GetPrevious());
    }

    [Fact]
    public void RemoveCurrent_GoToNext_RemovesNodeAndMovesForward()
    {
        UndoRedoHandler<string> handler = new();
        handler.AddNewNode("a");
        handler.AddNewNode("b");
        handler.AddNewNode("c");
        handler.MoveToPrevious();

        handler.RemoveCurrent(goToPrevious: false);

        Assert.Equal("c", handler.Current);
        Assert.Equal("a", handler.GetPrevious());
        Assert.Null(handler.GetNext());
    }

    [Fact]
    public void RemoveCurrent_Throws_WhenCurrentIsHeadOrTail()
    {
        UndoRedoHandler<string> atHead = new();
        Assert.Throws<InvalidOperationException>(() => atHead.RemoveCurrent(goToPrevious: true));

        UndoRedoHandler<string> atTail = new();
        atTail.AddNewNode("a");
        atTail.MoveToNext();

        Assert.True(atTail.IsTail());
        Assert.Throws<InvalidOperationException>(() => atTail.RemoveCurrent(goToPrevious: false));
    }

    [Fact]
    public void Enumerations_FromHeadAndTail_SkipSentinelNodes()
    {
        UndoRedoHandler<string> handler = new();
        handler.AddNewNode("a");
        handler.AddNewNode("b");
        handler.AddNewNode("c");

        handler.MoveToPrevious();
        handler.MoveToPrevious();
        handler.MoveToPrevious();

        Assert.True(handler.IsHead());
        Assert.Equal(["a", "b", "c"], handler.EnumerateFromCurrentForward().ToArray());

        handler.MoveToNext();
        handler.MoveToNext();
        handler.MoveToNext();
        handler.MoveToNext();

        Assert.True(handler.IsTail());
        Assert.Equal(["c", "b", "a"], handler.EnumerateFromCurrentBack().ToArray());
    }

    [Fact]
    public void AddNewNode_WhileCurrentAtTail_MovesBackThenAdds_AndInvokesCallbackForBothChanges()
    {
        int changedCount = 0;
        UndoRedoHandler<string> handler = new(() => changedCount++);
        handler.AddNewNode("a");
        handler.MoveToNext();

        handler.AddNewNode("b");

        Assert.Equal("b", handler.Current);
        Assert.Equal(["a"], handler.EnumerateFromCurrentBack().ToArray());
        Assert.Equal(4, changedCount);
    }
}
