namespace FileSurfer.Core.Models;

/// <summary>
/// Provides methods for manipulating paths for specific filesystems
/// </summary>
public interface IPathTools
{
    /// <summary>
    /// Platform-specific character used to
    /// separate directory levels in a path string
    /// </summary>
    public char DirSeparator { get; }

    /// <summary>
    /// Normalizes the given path to a path without redundant separators and without a trailing separator
    /// <para/>
    /// Separators at root level paths are kept.
    /// </summary>
    /// <param name="path">Path to normalize</param>
    /// <returns>Normalized path</returns>
    public string NormalizePath(string path);

    /// <summary>
    /// Combines two paths
    /// Returns the combined path without trailing separators
    /// </summary>
    public string Combine(string pathBase, string pathSuffix);

    /// <summary>
    /// Returns the parent directory of the given path.
    /// Trailing separators are ignored
    /// Returns <see cref="string.Empty"/> if path has no parent directory
    /// </summary>
    public string GetParentDir(string path);

    /// <summary>
    /// Returns the name and extension parts of the given path.
    /// The resulting string contains the characters of path that follow the last separator in path.
    /// Trailing separators are ignored
    /// </summary>
    public string GetFileName(string path);

    /// <summary>
    /// Returns extension part of the given path including the dot '.'
    /// Trailing separators are ignored.
    /// </summary>
    public string GetExtension(string path);

    /// <summary>
    /// Determines if two file or directory names are equal under the relevant filesystem's rules
    /// </summary>
    public bool NamesAreEqual(string? nameA, string? nameB);

    /// <summary>
    /// Determines if two file or directory paths are equal under the relevant filesystem's rules
    /// </summary>
    public bool PathsAreEqual(string? pathA, string? pathB);
}
