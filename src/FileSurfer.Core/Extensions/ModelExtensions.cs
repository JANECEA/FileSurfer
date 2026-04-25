using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FileSurfer.Core.Models;
using FileSurfer.Core.Services.Dialogs;
using FileSurfer.Core.Services.FileOperations;

namespace FileSurfer.Core.Extensions;

/// <summary>
/// Provides extension methods for working with file-system models.
/// </summary>
public static class FileSystemExtensions
{
    /// <summary>
    /// Creates a <see cref="Location"/> from a file system and path.
    /// </summary>
    /// <param name="fileSystem">The file system that owns the path.</param>
    /// <param name="path">The path within the file system.</param>
    /// <returns>The created location.</returns>
    public static Location GetLocation(this IFileSystem fileSystem, string path) =>
        new(fileSystem, path);

    /// <summary>
    /// Gets path tools associated with the location's file system.
    /// </summary>
    /// <param name="location">The location to resolve path tools for.</param>
    /// <returns>The path tools for the location.</returns>
    public static IPathTools PathTools(this Location location) =>
        location.FileSystem.FileInfoProvider.PathTools;
}

/// <summary>
/// Provides extension methods for working with result models.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Wraps a value into a successful <see cref="ValueResult{T}"/>.
    /// </summary>
    /// <param name="value">The value to wrap.</param>
    /// <returns>A successful result containing <paramref name="value"/>.</returns>
    public static ValueResult<T> OkResult<T>(this T value) => ValueResult<T>.Ok(value);

    /// <summary>
    /// Wraps a result into a completed task.
    /// </summary>
    /// <param name="result">The result to wrap.</param>
    /// <returns>A completed task containing <paramref name="result"/>.</returns>
    public static Task<IResult> ToTask(this IResult result) => Task.FromResult(result);

    /// <summary>
    /// Returns the first failed result from the provided sequence.
    /// </summary>
    /// <param name="results">The results to evaluate in order.</param>
    /// <returns>The first failed result, or <see langword="null"/> when all results are successful.</returns>
    public static IResult? FirstError(params IResult[] results)
    {
        foreach (IResult result in results)
            if (!result.IsOk)
                return result;

        return null;
    }
}

/// <summary>
/// Provides extension methods for transferring directory and file streams.
/// </summary>
public static class TransferStreamExtensions
{
    /// <summary>
    /// Writes a file transfer stream to the target stream.
    /// </summary>
    /// <param name="fileStream">The source file stream wrapper.</param>
    /// <param name="writeStream">The destination stream.</param>
    /// <param name="reporter">The progress reporter used for transfer updates.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The write result.</returns>
    public static async Task<IResult> WriteToStreamAsync(
        this FileTransferStream fileStream,
        Stream writeStream,
        ProgressReporter reporter,
        CancellationToken ct
    )
    {
        IndeterminateReporter rep = new(reporter);
        rep.ReportItem($"Transferring: \"{fileStream.Name}\"");

        try
        {
            ct.ThrowIfCancellationRequested();
            await fileStream.Stream.CopyToAsync(writeStream, ct);
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return ex is OperationCanceledException
                ? SimpleResult.Error("File transfer has been canceled.")
                : SimpleResult.Error(ex.Message);
        }
    }

    /// <summary>
    /// Writes a directory transfer stream using file I/O operations.
    /// </summary>
    /// <param name="dirStream">The source directory transfer stream.</param>
    /// <param name="ioHandler">The file I/O handler used for writes.</param>
    /// <param name="pathTools">The path tools used to combine target paths.</param>
    /// <param name="dirPath">The destination parent directory path.</param>
    /// <param name="reporter">The progress reporter used for transfer updates.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The write result.</returns>
    public static async Task<IResult> WriteWithIoHandlerAsync(
        this DirTransferStream dirStream,
        IFileIoHandler ioHandler,
        IPathTools pathTools,
        string dirPath,
        ProgressReporter reporter,
        CancellationToken ct
    )
    {
        (IResult result, bool rootCreated) = await WriteDirStreamInternal(
            dirStream,
            ioHandler,
            pathTools,
            dirPath,
            reporter,
            ct
        );
        if (!result.IsOk && rootCreated)
            _ = ioHandler.DeleteDir(pathTools.Combine(dirPath, dirStream.Name));

        return result;
    }

    private static async Task<(IResult, bool)> WriteDirStreamInternal(
        DirTransferStream dirStream,
        IFileIoHandler ioHandler,
        IPathTools pathTools,
        string dirPath,
        ProgressReporter reporter,
        CancellationToken ct
    )
    {
        List<(FileTransferStream, string)> files = new();
        Queue<(DirTransferStream, string)> queue = new();
        queue.Enqueue((dirStream, dirPath));
        bool rootCreated = false;

        while (queue.Count > 0)
        {
            (DirTransferStream dir, string absParentPath) = queue.Dequeue();
            string absDirPath = pathTools.Combine(absParentPath, dir.Name);

            IResult result = ioHandler.NewDirAt(absParentPath, dir.Name);
            if (!result.IsOk)
                return (result, rootCreated);

            rootCreated = true;
            foreach (FileTransferStream f in dir.Files)
                files.Add((f, absDirPath));

            foreach (DirTransferStream d in dir.Directories)
                queue.Enqueue((d, absDirPath));
        }

        CountingReporter rep = new(reporter, files.Count);
        foreach ((FileTransferStream stream, string parentPath) in files)
        {
            rep.ReportItem($"Transferring: \"{stream.Name}\"");
            IResult result = await ioHandler.WriteFileStreamAsync(
                stream,
                parentPath,
                ProgressReporter.None,
                ct
            );
            if (!result.IsOk)
                return (result, rootCreated);
        }

        return (SimpleResult.Ok(), rootCreated);
    }
}
