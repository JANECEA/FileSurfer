using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json.Serialization;

namespace FileSurfer.Core;

/// <summary>
/// Used to (de)serialize the settings.json file.
/// </summary>
[
    SuppressMessage("ReSharper", "InconsistentNaming"),
    JsonObjectCreationHandling(JsonObjectCreationHandling.Populate),
]
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
public record SettingsRecord
{
    private static IDefaultSettingsProvider? DefaultSettingsProvider;

    public string newImageName { get; set; }
    public string newFileName { get; set; }
    public string newDirectoryName { get; set; }
    public string thisPCLabel { get; set; }
    public string notepadApp { get; set; }
    public string terminal { get; set; }
    public bool openInLastLocation { get; set; }
    public string openIn { get; set; }
    public bool useDarkMode { get; set; }
    public string displayMode { get; set; }
    public string defaultSort { get; set; }
    public int fileSizeUnitLimit { get; set; }
    public bool sortReversed { get; set; }
    public bool showSpecialFolders { get; set; }
    public bool showProtectedFiles { get; set; }
    public bool showHiddenFiles { get; set; }
    public bool treatDotFilesAsHidden { get; set; }
    public bool gitIntegration { get; set; }
    public bool showUndoRedoErrorDialogs { get; set; }
    public bool automaticRefresh { get; set; }
    public int automaticRefreshInterval { get; set; }
    public bool allowImagePastingFromClipboard { get; set; }
    public List<string> quickAccess { get; set; }

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
