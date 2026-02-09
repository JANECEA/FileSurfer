using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FileSurfer.Core;

/// <summary>
/// Used to (de)serialize the settings.json file.
/// </summary>
[
    JsonObjectCreationHandling(JsonObjectCreationHandling.Populate),
    SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Json naming convention"),
    SuppressMessage(
        "ReSharper",
        "PropertyCanBeMadeInitOnly.Global",
        Justification = "Values need to be modifiable"
    ),
]
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
public record SettingsRecord
{
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
        AllowTrailingCommas = true,
        PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
    };

    private static IDefaultSettingsProvider? DefaultSettingsProvider;

    public string newImageName { get; set; } = "New Image";
    public string newFileName { get; set; } = "New File";
    public string newDirectoryName { get; set; } = "New Folder";
    public string notepadApp { get; set; } = string.Empty;
    public string notepadAppArgs { get; set; } = string.Empty;
    public string terminal { get; set; } = string.Empty;
    public string terminalArgs { get; set; } = string.Empty;
    public bool openInLastLocation { get; set; } = true;
    public string openIn { get; set; } = "";
    public bool useDarkMode { get; set; } = true;
    public string displayMode { get; set; } = nameof(DisplayMode.ListView);
    public string defaultSort { get; set; } = nameof(SortBy.Name);
    public int fileSizeUnitLimit { get; set; } = 4096;
    public bool sortReversed { get; set; } = false;
    public bool showSpecialFolders { get; set; } = true;
    public bool showProtectedFiles { get; set; } = false;
    public bool showHiddenFiles { get; set; } = true;
    public bool treatDotFilesAsHidden { get; set; } = true;
    public bool gitIntegration { get; set; } = true;
    public bool showUndoRedoErrorDialogs { get; set; } = true;
    public bool automaticRefresh { get; set; } = true;
    public int automaticRefreshInterval { get; set; } = 3000;
    public bool allowImagePastingFromClipboard { get; set; } = true;
    public List<string> quickAccess { get; set; } = new();

    public static void Initialize(IDefaultSettingsProvider defaultSettingsProvider) =>
        DefaultSettingsProvider = defaultSettingsProvider;

    public SettingsRecord()
    {
        if (DefaultSettingsProvider is null)
            throw new InvalidDataException("Settings need to be initialized during startup.");

        DefaultSettingsProvider.PopulateDefaults(this);
    }
}
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
