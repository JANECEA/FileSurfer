﻿namespace FileSurfer.Models.FileOperations;

/// <summary>
/// Represents the layer between the <see cref="FileSurfer"/> app and the system clipboard.
/// </summary>
public interface IClipboardManager
{
    /// <summary>
    /// Indicates if <see cref="_programClipboard"/>'s contents are meant to be cut or copied from their original location.
    /// </summary>
    public bool IsCutOperation { get; }

    /// <summary>
    /// Stores the selection of <see cref="IFileSystemEntry"/> in <see cref="_programClipboard"/> and the system clipboard.
    /// <para>
    /// Sets <see cref="IsCutOperation"/> to <see langword="false"/>.
    /// </para>
    /// </summary>
    /// <returns>A <see cref="IFileOperationResult"/> representing the result of the operation and potential errors.</returns>
    public IFileOperationResult Copy(IFileSystemEntry[] selectedFiles, string currentDir);

    /// <summary>
    /// Copies the <paramref name="filePath"/> to the system's clipboard.
    /// </summary>
    public void CopyPathToFile(string filePath);

    /// <summary>
    /// Stores <paramref name="selectedFiles"/> to both <see cref="Clipboard"/> and <see cref="_programClipboard"/>.
    /// <para>
    /// Sets <see cref="IsCutOperation"/> to <see langword="true"/>.
    /// </para>
    /// </summary>
    /// <returns>A <see cref="IFileOperationResult"/> representing the result of the operation and potential errors.</returns>
    public IFileOperationResult Cut(IFileSystemEntry[] selectedFiles, string currentDir);

    /// <summary>
    /// Duplicates the files stored in <see cref="_programClipboard"/>.
    /// </summary>
    /// <returns>A <see cref="IFileOperationResult"/> representing the result of the operation and potential errors.</returns>
    public IFileOperationResult Duplicate(string currentDir, out string[] copyNames);

    /// <summary>
    /// Gets the contents of <see cref="_programClipboard"/>.
    /// </summary>
    /// <returns>An array of <see cref="IFileSystemEntry"/>s.</returns>
    public IFileSystemEntry[] GetClipboard();

    /// <summary>
    /// Determines if the current copy operation is occuring in the same directory.
    /// </summary>
    /// <param name="currentDir"></param>
    /// <returns><see langword="true"/> if <see cref="_copyFromDir"/> and <paramref name="currentDir"/> are equal, otherwise <see langword="false"/>.</returns>
    public bool IsDuplicateOperation(string currentDir);

    /// <summary>
    /// Pastes the contents of the system clipboard into <paramref name="currentDir"/>.
    /// </summary>
    /// <returns>A <see cref="IFileOperationResult"/> representing the result of the operation and potential errors.</returns>
    public IFileOperationResult Paste(string currentDir);
}
