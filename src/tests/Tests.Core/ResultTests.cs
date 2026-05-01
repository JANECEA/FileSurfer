using FileSurfer.Core.Models;

namespace Tests.Core;

public class ResultTests
{
    [Fact]
    public void Ok_ReturnsSuccessful()
    {
        Result result = Result.Ok();

        Assert.True(result.IsOk);
        Assert.Empty(result.Errors);
    }

    public static TheoryData<string> SingleErrorCases =>
        new() { "Error 1", "Error 2", "Error with spaces and special chars!" };

    [Theory]
    [MemberData(nameof(SingleErrorCases))]
    public void Error_WithMessage_ReturnsFailed(string message)
    {
        Result result = Result.Error(message);

        Assert.False(result.IsOk);
        Assert.Single(result.Errors);
        Assert.Equal(message, result.Errors.First());
    }

    public static TheoryData<string[]> MultipleErrorCases =>
        new()
        {
            new[] { "error1", "error2" },
            new[] { "error1", "error2", "error3" },
            new[] { "a", "b", "c", "d" },
        };

    [Theory]
    [MemberData(nameof(MultipleErrorCases))]
    public void Error_WithMultipleMessages_ReturnsFailed(string[] messages)
    {
        Result result = Result.Error(messages);

        Assert.False(result.IsOk);
        Assert.Equal(messages.Length, result.Errors.Count());
        Assert.Equal(messages, result.Errors);
    }

    [Fact]
    public void Error_FromResult_CopiesErrors()
    {
        IResult source = SimpleResult.Error("source error");
        Result result = Result.Error(source);

        Assert.False(result.IsOk);
        Assert.Single(result.Errors);
        Assert.Equal("source error", result.Errors.First());
    }

    [Fact]
    public void AddError_ToSuccessful_MakesItFailed()
    {
        Result result = Result.Ok();
        result.AddError("new error");

        Assert.False(result.IsOk);
        Assert.Single(result.Errors);
        Assert.Equal("new error", result.Errors.First());
    }

    [Fact]
    public void AddError_ToFailed_AppendError()
    {
        Result result = Result.Error("error1");
        result.AddError("error2");

        Assert.False(result.IsOk);
        Assert.Equal(2, result.Errors.Count());
        Assert.Contains("error1", result.Errors);
        Assert.Contains("error2", result.Errors);
    }

    [Fact]
    public void AddError_MultipleInvocations_AllAdded()
    {
        Result result = Result.Ok();
        result.AddError("error1");
        result.AddError("error2");
        result.AddError("error3");

        Assert.False(result.IsOk);
        Assert.Equal(3, result.Errors.Count());
    }

    [Fact]
    public void MergeResult_FromSuccessful_NoChange()
    {
        Result result = Result.Error("error1");
        IResult other = SimpleResult.Ok();
        Result merged = result.MergeResult(other);

        Assert.Same(result, merged);
        Assert.Single(result.Errors);
        Assert.Equal("error1", result.Errors.First());
    }

    [Fact]
    public void MergeResult_FromFailed_MergesErrors()
    {
        Result result = Result.Error("error1");
        IResult other = SimpleResult.Error("error2");
        Result merged = result.MergeResult(other);

        Assert.Same(result, merged);
        Assert.Equal(2, result.Errors.Count());
        Assert.Contains("error1", result.Errors);
        Assert.Contains("error2", result.Errors);
    }

    [Fact]
    public void MergeResult_MultipleInvocations_ChainMerges()
    {
        Result result = Result.Error("error1");
        result.MergeResult(SimpleResult.Error("error2"));
        result.MergeResult(Result.Error(new[] { "error3", "error4" }));

        Assert.False(result.IsOk);
        Assert.Equal(4, result.Errors.Count());
        Assert.Contains("error1", result.Errors);
        Assert.Contains("error2", result.Errors);
        Assert.Contains("error3", result.Errors);
        Assert.Contains("error4", result.Errors);
    }
}
