using FileSurfer.Core.Models;
using FileSurfer.Linux.Models.FileInformation;

namespace Tests.Linux;

public class IconPathComparerTests
{
    public static TheoryData<IconPath?, IconPath?, string?, int> CompareCases =>
        new()
        {
            { null, null, null, 0 },
            { null, Path(), null, -1 },
            { Path(), null, null, 1 },
            { Path(), Path(), null, 0 },
            { Path(rest: "/theme/icons"), Path(rest: "/other/icons"), "theme", -1 },
            { Path(rest: "/other/icons"), Path(rest: "/theme/icons"), "theme", 1 },
            { Path(rest: "/my-theme-ish/icons"), Path(rest: "/other/icons"), "theme", -1 },
            { Path(rest: "/other/icons"), Path(rest: "/my-theme-ish/icons"), "theme", 1 },
            { Path(iconCount: 20), Path(iconCount: 10), null, -1 },
            { Path(iconCount: 10), Path(iconCount: 20), null, 1 },
            { Path(baseDir: "/home/u/.icons"), Path(baseDir: "/usr/share/icons"), null, -1 },
            { Path(baseDir: "/usr/share/icons"), Path(baseDir: "/home/u/.icons"), null, 1 },
            { Path(size: 64), Path(size: 128), null, -1 },
            { Path(size: 256), Path(size: 128), null, 1 },
            { Path(size: 64, iconCount: 9), Path(size: 64, iconCount: 9), null, 0 },
        };

    [Theory]
    [MemberData(nameof(CompareCases))]
    public void Compare_FollowsExpectedPriority(
        IconPath? a,
        IconPath? b,
        string? theme,
        int expectedSign
    )
    {
        IconPathComparer comparer = new(theme);

        int actual = Math.Sign(comparer.Compare(a, b));

        Assert.Equal(expectedSign, actual);
    }

    public static TheoryData<string?> SortCases => new() { "theme", null };

    [Theory]
    [MemberData(nameof(SortCases))]
    public void Compare_CanBeUsedToSortByDocumentedRules(string? theme)
    {
        List<IconPath> paths =
        [
            Path(
                size: 128,
                iconCount: 10,
                rest: "/hicolor/128x128/mimetypes",
                baseDir: "/usr/share/icons"
            ),
            Path(
                size: 64,
                iconCount: 5,
                rest: "/theme/64x64/mimetypes",
                baseDir: "/usr/share/icons"
            ),
            Path(
                size: 64,
                iconCount: 20,
                rest: "/hicolor/64x64/mimetypes",
                baseDir: "/home/u/.icons"
            ),
        ];

        paths.Sort(new IconPathComparer(theme));

        Assert.Equal(
            theme is null ? "/hicolor/64x64/mimetypes" : "/theme/64x64/mimetypes",
            paths[0].RestOfPath
        );
    }

    private static IconPath Path(
        int size = 64,
        int iconCount = 10,
        string rest = "/hicolor/64x64/mimetypes",
        string baseDir = "/usr/share/icons"
    ) => new(size, iconCount, rest, baseDir, $"{baseDir}{LocalPathTools.DirSeparator}icons");
}
