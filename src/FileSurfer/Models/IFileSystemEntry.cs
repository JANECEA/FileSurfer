namespace FileSurfer.Models;

/// <summary>
/// Represents a file system entry in context of the <see cref="FileSurfer"/> app.
/// </summary>
public interface IFileSystemEntry
{
    /// <summary>
    /// Path to the file, directory, or drive represented by this <see cref="IFileSystemEntry"/>.
    /// </summary>
    public string PathToEntry { get;}

    /// <summary>
    /// Holds the name of file, directory, or drive represented by this <see cref="IFileSystemEntry"/>.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Holds the extension of this file's name
    /// </summary>
    public string Extension { get; }

    /// <summary>
    /// Holds this entry's name without the extension
    /// </summary>
    public string NameWOExtension { get; }
}
