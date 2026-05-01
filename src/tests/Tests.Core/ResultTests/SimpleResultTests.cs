using FileSurfer.Core.Models;

namespace Tests.Core.ResultTests;

public class SimpleResultTests
{
    [Fact]
    public void Ok_ReturnsSuccessfulResult()
    {
        SimpleResult result = SimpleResult.Ok();

        Assert.True(result.IsOk);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Error_WithoutMessage_ReturnsFailed()
    {
        SimpleResult result = SimpleResult.Error();

        Assert.False(result.IsOk);
        Assert.Empty(result.Errors);
    }

    public static TheoryData<string> ErrorMessageCases =>
        new() { "Operation failed", "File not found", "Access denied" };

    [Theory]
    [MemberData(nameof(ErrorMessageCases))]
    public void Error_WithMessage_ReturnsFailed(string message)
    {
        SimpleResult result = SimpleResult.Error(message);

        Assert.False(result.IsOk);
        Assert.Single(result.Errors);
        Assert.Equal(message, result.Errors.First());
    }
}
