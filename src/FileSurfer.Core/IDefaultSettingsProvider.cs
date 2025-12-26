namespace FileSurfer.Core;

/// <summary>
/// Manages platform dependent default settings for FileSurfer.
/// </summary>
public interface IDefaultSettingsProvider
{
    /// <summary>
    /// Populates a <see cref="SettingsRecord"/> instance with the default settings
    /// </summary>
    public void PopulateDefaults(SettingsRecord settingsRecord);
}
