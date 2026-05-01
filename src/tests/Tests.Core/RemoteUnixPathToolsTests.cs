using FileSurfer.Core.Models;

namespace Tests.Core;

public class RemoteUnixPathToolsTests
{
    private const char S = RemoteUnixPathTools.DirSeparator;

    public static TheoryData<string, string> NormalizePathCases =>
        new()
        {
            { "", "" },
            { "   ", "   " },
            { "tmp/folder", "/tmp/folder" },
            { "/tmp//folder///", "/tmp/folder" },
            { "///tmp///folder", "/tmp/folder" },
            { "/", "/" },
        };

    [Theory]
    [MemberData(nameof(NormalizePathCases))]
    public void NormalizePath_ReturnsExpectedResult(string input, string expected)
    {
        Assert.Equal(expected, RemoteUnixPathTools.NormalizePath(input));
    }

    public static TheoryData<string, string, string> CombineCases =>
        new()
        {
            { $"{S}a", "b", $"{S}a{S}b" },
            { $"{S}a{S}", "b", $"{S}a{S}b" },
            { $"{S}a", $"{S}b", $"{S}a{S}b" },
            { $"{S}a{S}{S}", $"{S}{S}b{S}{S}", $"{S}a{S}b" },
        };

    [Theory]
    [MemberData(nameof(CombineCases))]
    public void Combine_TrimsAndJoinsWithSingleSeparator(
        string pathBase,
        string pathSuffix,
        string expected
    )
    {
        Assert.Equal(expected, RemoteUnixPathTools.Combine(pathBase, pathSuffix));
    }

    public static TheoryData<string, string> ParentDirCases =>
        new()
        {
            { "", "" },
            { "   ", "" },
            { "/", "" },
            { "/a", "/" },
            { "/a/b", "/a" },
            { "/a/b/", "/a" },
            { "a/b", "a" },
            { "single", "/" },
        };

    [Theory]
    [MemberData(nameof(ParentDirCases))]
    public void GetParentDir_ReturnsExpectedParent(string path, string expected)
    {
        Assert.Equal(expected, RemoteUnixPathTools.GetParentDir(path));
    }

    public static TheoryData<string, string, string> NameAndExtensionCases =>
        new()
        {
            { "/x/file.txt", "file.txt", ".txt" },
            { "/x/archive.tar.gz", "archive.tar.gz", ".gz" },
            { "/x/noext", "noext", "" },
            { "/x/dir/", "dir", "" },
            { "plain", "plain", "" },
            { "/x/.hidden", ".hidden", ".hidden" },
        };

    [Theory]
    [MemberData(nameof(NameAndExtensionCases))]
    public void GetFileNameAndExtension_ReturnExpectedValues(
        string path,
        string expectedName,
        string expectedExt
    )
    {
        Assert.Equal(expectedName, RemoteUnixPathTools.GetFileName(path));
        Assert.Equal(expectedExt, RemoteUnixPathTools.GetExtension(path));
    }

    public static TheoryData<string?, string?, bool> PathEqualityCases =>
        new()
        {
            { null, "/a", false },
            { "/a", null, false },
            { "/a/b", "/a//b/", true },
            { "/a/b", "/A/b", false },
            { "a/b", "/a/b", true },
        };

    [Theory]
    [MemberData(nameof(PathEqualityCases))]
    public void PathsAreEqual_UsesNormalizedOrdinalComparison(string? a, string? b, bool expected)
    {
        Assert.Equal(expected, RemoteUnixPathTools.PathsAreEqual(a, b));
    }

    public static TheoryData<string?, string?, bool> NameEqualityCases =>
        new()
        {
            { null, "x", false },
            { "x", null, false },
            { "name.txt", "name.txt", true },
            { "name.txt", "NAME.txt", false },
            { "name.txt", "other.txt", false },
        };

    [Theory]
    [MemberData(nameof(NameEqualityCases))]
    public void NamesAreEqual_UsesOrdinalComparison(string? a, string? b, bool expected)
    {
        Assert.Equal(expected, RemoteUnixPathTools.NamesAreEqual(a, b));
    }

    [Fact]
    public void IPathTools_Instance_DelegatesToStaticMethods()
    {
        IPathTools tools = RemoteUnixPathTools.Instance;

        Assert.Equal(RemoteUnixPathTools.DirSeparator, tools.DirSeparator);
        Assert.Equal("/a/b", tools.Combine("/a/", "b"));
        Assert.Equal("file.txt", tools.GetFileName("/x/file.txt"));
        Assert.Equal(".txt", tools.GetExtension("/x/file.txt"));
        Assert.True(tools.PathsAreEqual("/a/b", "/a//b/"));
    }
}
