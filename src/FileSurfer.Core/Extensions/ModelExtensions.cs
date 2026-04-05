using System.Collections.Generic;
using System.Threading;
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
    public static IResult WriteWithIoHandler(
        this DirTransferStream dirTransferStream,
        IFileIoHandler ioHandler,
        IPathTools pathTools,
        string dirPath,
        ProgressReporter reporter,
        CancellationToken ct
    )
    {
        Queue<(DirTransferStream, string)> queue = new();
        queue.Enqueue((dirTransferStream, dirPath));

        while (queue.Count > 0)
        {
            (DirTransferStream dir, string absParentPath) = queue.Dequeue();
            string absDirPath = pathTools.Combine(absParentPath, dir.Name);

            IResult result = ioHandler.NewDirAt(absParentPath, dir.Name);
            if (!result.IsOk)
                return result;

            foreach (FileTransferStream f in dir.Files)
            {
                result = ioHandler.WriteFileStream(f, absDirPath, reporter, ct);
                if (!result.IsOk)
                    return result;
            }

            foreach (DirTransferStream d in dir.Directories)
                queue.Enqueue((d, absDirPath));
        }

        return SimpleResult.Ok();
    }
}
