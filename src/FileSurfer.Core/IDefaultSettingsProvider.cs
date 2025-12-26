namespace FileSurfer.Core;

/// <summary>
/// Manages platform dependent default settings for FileSurfer.
/// </summary>
public interface IDefaultSettingsProvider
{
    /// <summary>
    /// Returns new platform specific default settings.
    /// </summary>
    public SettingsRecord GetDefaultSettings();
}
