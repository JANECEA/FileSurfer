using System.Reflection;
using FileSurfer.Core;
using FileSurfer.Core.ViewModels;
using Mocks;

namespace Tests.Core;

public class SettingsWindowViewModelTests
{
    private static readonly HashSet<string> UnmappedSettingsRecordProperties =
    [
        nameof(SettingsRecord.quickAccess),
        nameof(SettingsRecord.syncHiddenFiles),
    ];

    static SettingsWindowViewModelTests()
    {
        SettingsRecord.Initialize(new MockDefaultSettingsProvider());
    }

    [Fact]
    public void SettingsRecord_Properties_AreEitherMappedOrExplicitlyExcluded()
    {
        HashSet<string> vmPropertyNames = typeof(SettingsWindowViewModel)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);

        List<string> missingMappings = typeof(SettingsRecord)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => !UnmappedSettingsRecordProperties.Contains(p.Name))
            .Select(p => ToViewModelPropertyName(p.Name))
            .Where(vmName => !vmPropertyNames.Contains(vmName))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.Empty(missingMappings);
    }

    [Fact]
    public void ConstructorAndExtractSettings_RoundTripAllMappedProperties()
    {
        SettingsRecord source = BuildSettingsRecordWithDistinctValues();
        SettingsWindowViewModel vm = new(source);

        SettingsRecord extracted = vm.ExtractSettings();

        foreach (PropertyInfo settingsProperty in typeof(SettingsRecord).GetProperties())
        {
            if (UnmappedSettingsRecordProperties.Contains(settingsProperty.Name))
                continue;

            PropertyInfo vmProperty = typeof(SettingsWindowViewModel).GetProperty(
                ToViewModelPropertyName(settingsProperty.Name),
                BindingFlags.Public | BindingFlags.Instance
            )!;

            object? vmValue = vmProperty.GetValue(vm);
            object? extractedValue = settingsProperty.GetValue(extracted);

            Assert.Equal(vmValue, extractedValue);
        }
    }

    private static SettingsRecord BuildSettingsRecordWithDistinctValues()
    {
        SettingsRecord settings = new();

        int boolIndex = 0;
        int intIndex = 0;
        foreach (PropertyInfo property in typeof(SettingsRecord).GetProperties())
        {
            if (UnmappedSettingsRecordProperties.Contains(property.Name))
                continue;

            object value =
                property.PropertyType == typeof(string)
                    ? property.Name switch
                    {
                        nameof(SettingsRecord.displayMode) => nameof(DisplayMode.IconView),
                        nameof(SettingsRecord.defaultSort) => nameof(SortBy.Size),
                        _ => $"{property.Name}-value",
                    }
                : property.PropertyType == typeof(bool) ? boolIndex++ % 2 == 0
                : property.PropertyType == typeof(int) ? 1200 + intIndex++
                : throw new NotSupportedException(property.PropertyType.FullName);

            property.SetValue(settings, value);
        }

        return settings;
    }

    private static string ToViewModelPropertyName(string settingsPropertyName) =>
        char.ToUpperInvariant(settingsPropertyName[0]) + settingsPropertyName[1..];
}
