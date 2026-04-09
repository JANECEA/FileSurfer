using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FileSurfer.Core.Models;
using FileSurfer.Core.Services.Dialogs;
using FileSurfer.Core.Services.FileOperations;

namespace FileSurfer.Core.Extensions;

public static class FileSystemExtensions
{
    public static Location GetLocation(this IFileSystem fileSystem, string path) =>
        new(fileSystem, path);
}

public static class ResultExtensions
{
    /// <summary>
    /// Turns value into an Ok <see cref="ValueResult{T}"/>
    /// </summary>
    public static ValueResult<T> OkResult<T>(this T value) => ValueResult<T>.Ok(value);

    public static Task<IResult> ToTask(this IResult result) => Task.FromResult(result);

    /// <summary>
    /// Return first failed result
    /// </summary>
    public static IResult? FirstError(params IResult[] results)
    {
        foreach (IResult result in results)
            if (!result.IsOk)
                return result;

        return null;
    }
}

public static class TransferStreamExtensions
{
    public static async Task<IResult> WriteToStreamAsync(
        this FileTransferStream fileStream,
        Stream writeStream,
        string filePath,
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

    public static async Task<IResult> WriteWithIoHandlerAsync(
        this DirTransferStream dirStream,
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

        while (queue.Count > 0)
        {
            (DirTransferStream dir, string absParentPath) = queue.Dequeue();
            string absDirPath = pathTools.Combine(absParentPath, dir.Name);

            IResult result = ioHandler.NewDirAt(absParentPath, dir.Name);
            if (!result.IsOk)
                return result;

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
                return result;
        }

        return SimpleResult.Ok();
    }
}
