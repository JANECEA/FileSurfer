using System.Reflection;
using FileSurfer.Core;
using Mocks;

namespace Tests.Core;

public class FileSurferSettingsTests
{
    private static readonly Dictionary<string, string> SettingsToStaticMappings = new(
        StringComparer.Ordinal
    )
    {
        [nameof(SettingsRecord.defaultSort)] = nameof(FileSurferSettings.SortingMode),
        [nameof(SettingsRecord.displayMode)] = nameof(FileSurferSettings.DisplayMode),
    };

    static FileSurferSettingsTests()
    {
        SettingsRecord.Initialize(new MockDefaultSettingsProvider());
    }

    [Fact]
    public void SettingsRecord_Properties_MapTo_FileSurferSettings_StaticMembers()
    {
        HashSet<string> staticPropertyNames = typeof(FileSurferSettings)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);

        List<string> missingMappings = typeof(SettingsRecord)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => ResolveStaticPropertyName(p.Name))
            .Where(staticName => !staticPropertyNames.Contains(staticName))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.Empty(missingMappings);
    }

    [Fact]
    public void ImportSettings_AndCurrentSettings_RoundTripAllSettingsRecordValues()
    {
        SettingsRecord source = BuildRoundTripSettings();

        FileSurferSettings.ImportSettings(source);
        SettingsRecord current = FileSurferSettings.CurrentSettings;

        foreach (PropertyInfo settingsProperty in typeof(SettingsRecord).GetProperties())
        {
            object? expected = settingsProperty.GetValue(source);
            object? actual = settingsProperty.GetValue(current);

            if (settingsProperty.PropertyType == typeof(List<string>))
                Assert.Equal((List<string>)expected!, (List<string>)actual!);
            else
                Assert.Equal(expected, actual);
        }
    }

    public static TheoryData<int, int> FileSizeClampCases =>
        new()
        {
            {
                FileSurferSettings.FileSizeUnitLimitLowerBound - 1,
                FileSurferSettings.FileSizeUnitLimitLowerBound
            },
            {
                FileSurferSettings.FileSizeUnitLimitUpperBound + 1,
                FileSurferSettings.FileSizeUnitLimitUpperBound
            },
        };

    [Theory]
    [MemberData(nameof(FileSizeClampCases))]
    public void ImportSettings_Clamps_FileSizeUnitLimit(int input, int expected)
    {
        SettingsRecord settings = BuildRoundTripSettings();
        settings.fileSizeUnitLimit = input;

        FileSurferSettings.ImportSettings(settings);

        Assert.Equal(expected, FileSurferSettings.FileSizeUnitLimit);
    }

    public static TheoryData<int, int> RefreshIntervalClampCases =>
        new()
        {
            {
                FileSurferSettings.AutomaticRefreshIntervalLowerBound - 1,
                FileSurferSettings.AutomaticRefreshIntervalLowerBound
            },
            {
                FileSurferSettings.AutomaticRefreshIntervalUpperBound + 1,
                FileSurferSettings.AutomaticRefreshIntervalUpperBound
            },
        };

    [Theory]
    [MemberData(nameof(RefreshIntervalClampCases))]
    public void ImportSettings_Clamps_AutomaticRefreshInterval(int input, int expected)
    {
        SettingsRecord settings = BuildRoundTripSettings();
        settings.automaticRefreshInterval = input;

        FileSurferSettings.ImportSettings(settings);

        Assert.Equal(expected, FileSurferSettings.AutomaticRefreshInterval);
    }

    public static TheoryData<int, int> SynchronizerIntervalClampCases =>
        new()
        {
            {
                FileSurferSettings.SynchronizerPollingIntervalLowerBound - 1,
                FileSurferSettings.SynchronizerPollingIntervalLowerBound
            },
            {
                FileSurferSettings.SynchronizerPollingIntervalUpperBound + 1,
                FileSurferSettings.SynchronizerPollingIntervalUpperBound
            },
        };

    [Theory]
    [MemberData(nameof(SynchronizerIntervalClampCases))]
    public void ImportSettings_Clamps_SynchronizerPollingInterval(int input, int expected)
    {
        SettingsRecord settings = BuildRoundTripSettings();
        settings.synchronizerPollingInterval = input;

        FileSurferSettings.ImportSettings(settings);

        Assert.Equal(expected, FileSurferSettings.SynchronizerPollingInterval);
    }

    private static string ResolveStaticPropertyName(string settingsPropertyName) =>
        SettingsToStaticMappings.TryGetValue(settingsPropertyName, out string? mapped)
            ? mapped
            : char.ToUpperInvariant(settingsPropertyName[0]) + settingsPropertyName[1..];

    private static SettingsRecord BuildRoundTripSettings() =>
        new()
        {
            newImageName = "new-image",
            newTextFileName = "new-text",
            newFileName = "new-file",
            newDirectoryName = "new-dir",
            notepadApp = "/usr/bin/vim",
            notepadAppArgs = "--clean",
            terminal = "/usr/bin/bash",
            terminalArgs = "-lc",
            openInLastLocation = false,
            openIn = "/tmp",
            useDarkMode = false,
            displayMode = nameof(DisplayMode.IconView),
            defaultSort = nameof(SortBy.Size),
            fileSizeUnitLimit = FileSurferSettings.FileSizeUnitLimitLowerBound + 3,
            sortReversed = true,
            showSpecialFolders = false,
            showProtectedFiles = true,
            showHiddenFiles = false,
            treatDotFilesAsHidden = false,
            gitIntegration = false,
            showUndoRedoErrorDialogs = false,
            automaticRefresh = false,
            automaticRefreshInterval = FileSurferSettings.AutomaticRefreshIntervalLowerBound + 100,
            synchronizerPollingInterval =
                FileSurferSettings.SynchronizerPollingIntervalLowerBound + 100,
            quickAccess = ["/a", "/b"],
            syncHiddenFiles = true,
        };
}
