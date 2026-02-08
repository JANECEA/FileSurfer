namespace FileSurfer.Core.Models.VersionControl;

/// <summary>
/// Consolidates complex Git file handling for the <see cref="FileSurfer"/> app's UI.
/// </summary>
public enum GitStatus
{
    /// <summary>
    /// File is either ignored or no changes have been made to it from the last commit.
    /// </summary>
    NotVersionControlled,

    /// <summary>
    /// File has been staged for the next commit.
    /// </summary>
    Staged,

    /// <summary>
    /// File has not been staged for the next commit.
    /// </summary>
    Unstaged,
}
