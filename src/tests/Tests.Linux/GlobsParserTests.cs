using System.Text;
using FileSurfer.Linux.Models.FileInformation;

// ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local

namespace Tests.Linux;

public class GlobsParserTests
{
    public static TheoryData<string[], string[], string[]> ParseCases =>
        new()
        {
            { ["text/plain:*.txt"], ["txt"], ["text-plain"] },
            {
                ["application/x-tar:*.tar.gz", "text/markdown:*.md"],
                ["tar.gz", "md"],
                ["application-x-tar", "text-markdown"]
            },
            {
                ["50:text/plain:*.txt", "60:text/plain:*.txt", "image/png:*.png"],
                ["txt", "png"],
                ["50:text-plain", "image-png"]
            },
            {
                [
                    "# comment",
                    "",
                    "application/json:*.json",
                    "text/plain:*.txt",
                    "# trailing comment",
                ],
                ["json", "txt"],
                ["application-json", "text-plain"]
            },
            {
                ["text/plain:*.txt", "invalid-line-without-colon", "text/xml:*", "text/csv:*.csv"],
                ["txt", "csv"],
                ["text-plain", "text-csv"]
            },
        };

    [Theory]
    [MemberData(nameof(ParseCases))]
    public void Parse_ReturnsExpectedMappings(
        string[] lines,
        string[] expectedKeys,
        string[] expectedValues
    )
    {
        using StreamReader reader = BuildReader(lines);

        Dictionary<string, string> result = GlobsParser.Parse(reader);

        Assert.Equal(expectedKeys.Length, result.Count);
        for (int i = 0; i < expectedKeys.Length; i++)
        {
            Assert.True(result.TryGetValue(expectedKeys[i], out string? value));
            Assert.Equal(expectedValues[i], value);
        }
    }

    public static TheoryData<string[]> MalformedInputCases =>
        new()
        {
            { [] },
            { [""] },
            { ["#", "##", "# just comment"] },
            { ["no-colon-no-star"] },
            { [":*.txt"] },
            { ["text/plain:"] },
            { ["text/plain:*"] },
            { ["text/plain:*txt"] },
            { ["text/plain:*."] },
            { ["text/plain:*.", "text/plain:*."] },
            { ["text/plain::"] },
            { [":::"] },
            { ["*:*.txt"] },
            { ["a:*", "b:", ":c"] },
            { ["text/plain:*.txt", ":", "*", ":::", "text/plain:*"] },
            { ["\0\0\0", "text/plain:\0*.txt"] },
            { ["text/plain:*.txt:extra", "mime/with/slash:*.ext:with:colons"] },
        };

    [Theory]
    [MemberData(nameof(MalformedInputCases))]
    public void Parse_DoesNotThrow_ForMalformedInputs(string[] lines)
    {
        using StreamReader reader = BuildReader(lines);

        Dictionary<string, string> result = GlobsParser.Parse(reader);

        Assert.All(
            result,
            kvp =>
            {
                Assert.False(string.IsNullOrEmpty(kvp.Key));
                Assert.False(string.IsNullOrEmpty(kvp.Value));
            }
        );
    }

    [Fact]
    public void Parse_IgnoresDuplicateExtensions_AfterFirstMatch()
    {
        using StreamReader reader = BuildReader([
            "text/plain:*.txt",
            "application/x-ignored:*.txt",
            "application/json:*.json",
            "application/x-ignored-too:*.json",
        ]);

        Dictionary<string, string> result = GlobsParser.Parse(reader);

        Assert.Equal(2, result.Count);
        Assert.Equal("text-plain", result["txt"]);
        Assert.Equal("application-json", result["json"]);
    }

    private static StreamReader BuildReader(IEnumerable<string> lines)
    {
        string content = string.Join('\n', lines);
        MemoryStream stream = new(Encoding.UTF8.GetBytes(content));
        return new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false);
    }
}
