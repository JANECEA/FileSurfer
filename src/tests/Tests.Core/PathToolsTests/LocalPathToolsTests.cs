using FileSurfer.Core.Models;

namespace Tests.Core.PathToolsTests;

public class LocalPathToolsTests
{
    private static readonly char S = LocalPathTools.DirSeparator;

    public static TheoryData<string, string> NormalizePathCases
    {
        get
        {
            string root = Path.GetPathRoot(Path.GetFullPath($"{S}")) ?? $"{S}";
            string tmpA = Path.Join(root, "tmp", "a");
            string tmpAA = Path.Join(root, "tmp", "a", "a");

            return new TheoryData<string, string>
            {
                { "", "" },
                { "   ", "   " },
                { $"{S}tmp{S}a{S}", tmpA },
                { $"{S}tmp{S}{S}a{S}{S}a", tmpAA },
            };
        }
    }

    [Theory]
    [MemberData(nameof(NormalizePathCases))]
    public void NormalizePath_ReturnsExpectedResult(string input, string expected)
    {
        Assert.Equal(expected, LocalPathTools.NormalizePath(input));
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
        Assert.Equal(expected, LocalPathTools.Combine(pathBase, pathSuffix));
    }

    public static TheoryData<string, string, string> NameAndExtensionCases =>
        new()
        {
            { $"{S}x{S}file.txt", "file.txt", ".txt" },
            { $"{S}x{S}archive.tar.gz", "archive.tar.gz", ".gz" },
            { $"{S}x{S}noext", "noext", "" },
            { $"{S}x{S}dir{S}", "dir", "" },
        };

    [Theory]
    [MemberData(nameof(NameAndExtensionCases))]
    public void GetFileNameAndExtension_ReturnExpectedValues(
        string path,
        string expectedName,
        string expectedExt
    )
    {
        Assert.Equal(expectedName, LocalPathTools.GetFileName(path));
        Assert.Equal(expectedExt, LocalPathTools.GetExtension(path));
    }

    public static TheoryData<string, string> ParentDirCases
    {
        get
        {
            char s = S;
            return new TheoryData<string, string>
            {
                { $"{s}a{s}b{s}c.txt", $"{s}a{s}b" },
                { $"{s}a{s}b{s}", $"{s}a" },
                { $"relative{s}file.txt", "relative" },
            };
        }
    }

    [Theory]
    [MemberData(nameof(ParentDirCases))]
    public void GetParentDir_ReturnsExpectedParent(string path, string expectedParent)
    {
        Assert.Equal(expectedParent, LocalPathTools.GetParentDir(path));
    }

    [Fact]
    public void EnumerateExtensions_ReturnsAllExtensionSegments()
    {
        string[] result = LocalPathTools.EnumerateExtensions($"{S}x{S}archive.tar.gz").ToArray();

        Assert.Equal(["tar.gz", "gz"], result);
    }

    public static TheoryData<string?, string?, bool> PathEqualityCases =>
        new()
        {
            { null, $"{S}a", false },
            { $"{S}a", null, false },
            { $"{S}a{S}b", $"{S}a{S}{S}b{S}", true },
            { $"{S}a{S}b", $"{S}a{S}c", false },
        };

    [Theory]
    [MemberData(nameof(PathEqualityCases))]
    public void PathsAreEqual_UsesNormalizedComparison(string? a, string? b, bool expected)
    {
        Assert.Equal(expected, LocalPathTools.PathsAreEqual(a, b));
    }

    [Fact]
    public void PathsAreEqualNormalized_UsesConfiguredStringComparison()
    {
        string a = OperatingSystem.IsWindows() ? @"C:\A\B" : "/a/b";
        string b = OperatingSystem.IsWindows() ? @"c:\a\b" : "/a/b";

        Assert.Equal(
            string.Equals(a, b, LocalPathTools.Comparison),
            LocalPathTools.PathsAreEqualNormalized(a, b)
        );
    }

    public static TheoryData<string?, string?> NameEqualityCases =>
        new()
        {
            { null, "x" },
            { "x", null },
            { "name.txt", "name.txt" },
            { "name.txt", "NAME.txt" },
            { "name.txt", "other.txt" },
        };

    [Theory]
    [MemberData(nameof(NameEqualityCases))]
    public void NamesAreEqual_UsesConfiguredStringComparison(string? a, string? b)
    {
        bool expected =
            a is not null && b is not null && string.Equals(a, b, LocalPathTools.Comparison);
        Assert.Equal(expected, LocalPathTools.NamesAreEqual(a, b));
    }
}
