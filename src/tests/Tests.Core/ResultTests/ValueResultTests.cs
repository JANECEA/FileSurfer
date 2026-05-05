using FileSurfer.Core.Models;

namespace Tests.Core.ResultTests;

public class ValueResultTests
{
    public static TheoryData<int> OkValueCases => new() { 0, 42, -1, int.MaxValue };

    [Theory]
    [MemberData(nameof(OkValueCases))]
    public void Ok_WithValue_ReturnsSuccessful(int value)
    {
        ValueResult<int> result = ValueResult<int>.Ok(value);

        Assert.True(result.IsOk);
        Assert.Equal(value, result.Value);
        Assert.Empty(result.Errors);
    }

    public static TheoryData<string> OkStringCases => new() { "", "value", "path/to/file" };

    [Theory]
    [MemberData(nameof(OkStringCases))]
    public void Ok_WithString_ReturnsSuccessful(string value)
    {
        ValueResult<string> result = ValueResult<string>.Ok(value);

        Assert.True(result.IsOk);
        Assert.Equal(value, result.Value);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Ok_WithNull_ReturnsSuccessful()
    {
        ValueResult<object?> result = ValueResult<object?>.Ok(null);

        Assert.True(result.IsOk);
        Assert.Null(result.Value);
    }

    [Fact]
    public void Error_WithoutMessage_ReturnsFailed()
    {
        ValueResult<int> result = ValueResult<int>.Error();

        Assert.False(result.IsOk);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Error_WithoutMessage_ThrowsWhenAccessingValue()
    {
        ValueResult<int> result = ValueResult<int>.Error();

        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Error_WithMessage_ReturnsFailed()
    {
        ValueResult<int> result = ValueResult<int>.Error("Operation failed");

        Assert.False(result.IsOk);
        Assert.Single(result.Errors);
        Assert.Equal("Operation failed", result.Errors.First());
    }

    [Fact]
    public void Error_WithMessage_ThrowsWhenAccessingValue()
    {
        ValueResult<int> result = ValueResult<int>.Error("error message");

        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Error_FromFailedResult_CopiesErrors()
    {
        IResult source = SimpleResult.Error("source error");
        ValueResult<int> result = ValueResult<int>.Error(source);

        Assert.False(result.IsOk);
        Assert.Single(result.Errors);
        Assert.Equal("source error", result.Errors.First());
    }

    [Fact]
    public void Error_FromSuccessfulResult_Throws()
    {
        IResult source = SimpleResult.Ok();

        Assert.Throws<InvalidOperationException>(() => ValueResult<int>.Error(source));
    }

    [Fact]
    public void Value_ThrowsWhenNotOk()
    {
        ValueResult<string> result = ValueResult<string>.Error("error");

        Assert.Throws<InvalidOperationException>(() => result.Value);
    }
}
