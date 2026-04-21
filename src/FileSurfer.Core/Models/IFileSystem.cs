using System;
using FileSurfer.Core.Models.FileInformation;
using FileSurfer.Core.Services.FileOperations;
using FileSurfer.Core.Services.Shell;
using FileSurfer.Core.Services.VersionControl;

namespace FileSurfer.Core.Models;

/// <summary>
/// Represents a generic file system and provides its functionality
/// </summary>
public interface IFileSystem : IDisposable
{
    /// <summary>
    /// Gets the provider used to retrieve file information.
    /// </summary>
    public IFileInfoProvider FileInfoProvider { get; }

    /// <summary>
    /// Gets the provider used to resolve file and directory icons.
    /// </summary>
    public IIconProvider IconProvider { get; }

    /// <summary>
    /// Gets the service used to manage archive operations.
    /// </summary>
    public IArchiveManager ArchiveManager { get; }

    /// <summary>
    /// Gets the service used for file I/O operations.
    /// </summary>
    public IFileIoHandler FileIoHandler { get; }

    /// <summary>
    /// Gets the service used to interact with the recycle bin or trash.
    /// </summary>
    public IBinInteraction BinInteraction { get; }

    /// <summary>
    /// Gets the service used to read file and directory properties.
    /// </summary>
    public IFileProperties FileProperties { get; }

    /// <summary>
    /// Gets the shell integration handler for this file system.
    /// </summary>
    public IShellHandler ShellHandler { get; }

    /// <summary>
    /// Gets the version control integration for this file system.
    /// </summary>
    public IGitIntegration GitIntegration { get; }

    /// <summary>
    /// Determines whether the file system is ready for use.
    /// </summary>
    /// <returns><see langword="true"/> when the file system is ready; otherwise, <see langword="false"/>.</returns>
    public bool IsReady();

    /// <summary>
    /// Determines whether this file system is local.
    /// </summary>
    /// <returns><see langword="true"/> when this file system is local; otherwise, <see langword="false"/>.</returns>
    public bool IsLocal();

    /// <summary>
    /// Determines whether the specified file system is the same instance as this one.
    /// </summary>
    /// <param name="fileSystem">The file system instance to compare with this instance.</param>
    /// <returns><see langword="true"/> when both references point to the same instance; otherwise, <see langword="false"/>.</returns>
    public bool IsSame(IFileSystem? fileSystem) => ReferenceEquals(this, fileSystem);

    /// <summary>
    /// Gets a label that identifies this file system.
    /// </summary>
    /// <returns>The file system label.</returns>
    public string GetLabel();
}

/// <summary>
/// Represents a local file system with more specialized versions of <see cref="IFileSystem"/>'s models
/// </summary>
public sealed class LocalFileSystem : IFileSystem
{
    private const string Label = "local";

    IFileInfoProvider IFileSystem.FileInfoProvider => LocalFileInfoProvider;
    IShellHandler IFileSystem.ShellHandler => LocalShellHandler;

    /// <summary>
    /// Gets the local file information provider used by this file system.
    /// </summary>
    public required ILocalFileInfoProvider LocalFileInfoProvider { get; init; }

    /// <summary>
    /// Gets the local shell integration handler.
    /// </summary>
    public required ILocalShellHandler LocalShellHandler { get; init; }

    public required IIconProvider IconProvider { get; init; }

    public required IArchiveManager ArchiveManager { get; init; }

    public required IFileIoHandler FileIoHandler { get; init; }

    public required IBinInteraction BinInteraction { get; init; }

    public required IFileProperties FileProperties { get; init; }

    public required IGitIntegration GitIntegration { get; init; }

    public bool IsReady() => true;

    public bool IsLocal() => true;

    public string GetLabel() => Label;

    public void Dispose()
    {
        IconProvider.Dispose();
        BinInteraction.Dispose();
        GitIntegration.Dispose();
    }
}
