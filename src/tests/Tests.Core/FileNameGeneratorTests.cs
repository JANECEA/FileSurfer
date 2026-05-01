using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileInformation;
using Mocks;

namespace Tests.Core;

public sealed class InMemoryExistsFileInfoProvider : MockFileInfoProvider
{
    private readonly HashSet<string> _existingPaths = [];

    public void AddExisting(string directory, IEnumerable<string> names)
    {
        foreach (string name in names)
            _existingPaths.Add(LocalPathTools.Combine(directory, name));
    }

    public override ExistsInfo Exists(string path)
    {
        RecordCall(nameof(Exists), path);
        return _existingPaths.Contains(path)
            ? ExistsInfo.ExistsAsFile()
            : ExistsInfo.DoesNotExist();
    }

    public override Task<ExistsInfo> ExistsAsync(string path)
    {
        RecordCall(nameof(ExistsAsync), path);
        return Task.FromResult(Exists(path));
    }
}

public class FileNameGeneratorTests
{
    public static TheoryData<string, string[]> AvailableNameCases =>
        new()
        {
            { "report.txt", [] },
            { "report.txt", ["report.txt"] },
            { "report.txt", ["report.txt", "report (1).txt"] },
            { "archive.tar.gz", ["archive.tar.gz", "archive.tar (1).gz"] },
        };

    [Theory]
    [MemberData(nameof(AvailableNameCases))]
    public void GetAvailableName_GeneratesExpectedSuffixes(
        string inputName,
        string[] existingInDirectory
    )
    {
        const string dir = "/work";
        InMemoryExistsFileInfoProvider provider = new();
        provider.AddExisting(dir, existingInDirectory);

        string actual = FileNameGenerator.GetAvailableName(provider, dir, inputName);

        string expected = inputName;
        int suffix = 0;
        while (existingInDirectory.Contains(expected))
        {
            suffix++;
            string noExt = Path.GetFileNameWithoutExtension(inputName);
            string ext = Path.GetExtension(inputName);
            expected = $"{noExt} ({suffix}){ext}";
        }
        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(AvailableNameCases))]
    public async Task GetAvailableNameAsync_MatchesSyncBehavior(
        string inputName,
        string[] existingInDirectory
    )
    {
        const string dir = "/work";
        InMemoryExistsFileInfoProvider provider = new();
        provider.AddExisting(dir, existingInDirectory);

        string syncResult = FileNameGenerator.GetAvailableName(provider, dir, inputName);
        string asyncResult = await FileNameGenerator.GetAvailableNameAsync(
            provider,
            dir,
            inputName
        );

        Assert.Equal(syncResult, asyncResult);
    }

    [Fact]
    public void GetNameMultipleDirs_EnsuresNameIsAvailableInAllDirectories()
    {
        const string name = "file.txt";
        const string dirA = "/a";
        const string dirB = "/b";
        const string dirC = "/c";
        InMemoryExistsFileInfoProvider provider = new();
        provider.AddExisting(dirA, [name, "file (1).txt"]);
        provider.AddExisting(dirB, [name]);
        provider.AddExisting(dirC, []);

        string actual = FileNameGenerator.GetNameMultipleDirs(provider, name, dirA, dirB, dirC);

        Assert.Equal("file (2).txt", actual);
    }

    public static TheoryData<string, string, string> CopyNameCases =>
        new()
        {
            { "/src/file.txt", "/dst", "file - Copy.txt" },
            { "/src/archive.tar.gz", "/dst", "archive.tar - Copy.gz" },
            { "/src/folder", "/dst", "folder - Copy" },
        };

    [Theory]
    [MemberData(nameof(CopyNameCases))]
    public void GetCopyName_GeneratesCopyNameForFilesAndDirectories(
        string entryPath,
        string targetDir,
        string expectedBaseName
    )
    {
        InMemoryExistsFileInfoProvider provider = new();
        IPathTools pathTools = provider.PathTools;
        IFileSystemEntry entry = Path.HasExtension(entryPath)
            ? new FileEntry(entryPath, pathTools)
            : new DirectoryEntry(entryPath, pathTools);

        string actual = FileNameGenerator.GetCopyName(provider, targetDir, entry);

        Assert.Equal(expectedBaseName, actual);
    }

    [Fact]
    public void GetAvailableNames_ReturnsEmptyArray_WhenNoEntries()
    {
        InMemoryExistsFileInfoProvider provider = new();

        string[] result = FileNameGenerator.GetAvailableNames(provider, [], "renamed.txt");

        Assert.Empty(result);
    }

    [Fact]
    public void GetAvailableNames_Throws_WhenFirstEntryHasNoParentDirectory()
    {
        InMemoryExistsFileInfoProvider provider = new();
        IPathTools pathTools = provider.PathTools;
        List<IFileSystemEntry> entries = [new FileEntry("just-a-file.txt", pathTools)];

        Assert.Throws<ArgumentException>(() =>
            FileNameGenerator.GetAvailableNames(provider, entries, "renamed.txt")
        );
    }

    [Fact]
    public void GetAvailableNames_GeneratesSequentialUniqueNames()
    {
        const string dir = "/work";
        InMemoryExistsFileInfoProvider provider = new();
        provider.AddExisting(dir, ["renamed (1).txt", "renamed (3).txt"]);
        IPathTools pathTools = provider.PathTools;

        List<IFileSystemEntry> entries =
        [
            new FileEntry("/work/old-a.txt", pathTools),
            new FileEntry("/work/old-b.txt", pathTools),
            new FileEntry("/work/old-c.txt", pathTools),
        ];

        string[] result = FileNameGenerator.GetAvailableNames(provider, entries, "renamed.txt");

        Assert.Equal(["renamed (2).txt", "renamed (4).txt", "renamed (5).txt"], result);
    }
}
