using System;
using System.Collections.Generic;
using System.IO;
using FileSurfer.Core.Extensions;
using FileSurfer.Core.Models.FileInformation;

namespace FileSurfer.Core.Models;

/// <summary>
/// Defines a named stream used for file transfer.
/// </summary>
public class FileTransferStream : IDisposable
{
    public Stream Stream { get; }
    public string Name { get; }

    public FileTransferStream(string name, Stream stream)
    {
        Stream = stream;
        Name = name;
    }

    public static ValueResult<FileTransferStream> FromInfoProvider(
        IFileInfoProvider fileInfoProvider,
        string path
    )
    {
        ValueResult<Stream> streamR = fileInfoProvider.GetFileStream(path);
        if (!streamR.IsOk)
            return ValueResult<FileTransferStream>.Error(streamR);

        return new FileTransferStream(
            fileInfoProvider.PathTools.GetFileName(path),
            streamR.Value
        ).OkResult();
    }

    public void Dispose() => Stream.Dispose();
}

/// <summary>
/// Defines a tree of <see cref="FileTransferStream"/> used for directory trasnfer.
/// </summary>
public class DirTransferStream : IDisposable
{
    public List<DirTransferStream> Directories { get; }

    public List<FileTransferStream> Files { get; }

    public string Name { get; }

    private DirTransferStream(string name)
    {
        Directories = new List<DirTransferStream>();
        Files = new List<FileTransferStream>();
        Name = name;
    }

    public void Dispose()
    {
        foreach (FileTransferStream file in Files)
            file.Dispose();

        Queue<DirTransferStream> queue = new(Directories);
        while (queue.Count > 0)
        {
            DirTransferStream current = queue.Dequeue();

            foreach (FileTransferStream file in current.Files)
                file.Dispose();

            foreach (DirTransferStream dir in current.Directories)
                queue.Enqueue(dir);
        }
    }

    public static ValueResult<DirTransferStream> FromInfoProvider(
        IFileInfoProvider fileInfoProvider,
        string rootPath,
        bool includeHidden,
        bool includeOs
    )
    {
        DirTransferStream root = new(fileInfoProvider.PathTools.GetFileName(rootPath));
        var result = FromInfoInternal(fileInfoProvider, rootPath, includeHidden, includeOs, root);
        if (!result.IsOk)
            root.Dispose();

        return result;
    }

    private static ValueResult<DirTransferStream> FromInfoInternal(
        IFileInfoProvider fileInfoProvider,
        string rootPath,
        bool includeHidden,
        bool includeOs,
        DirTransferStream root
    )
    {
        Queue<(DirectoryEntry, DirTransferStream)> queue = new();
        queue.Enqueue((new DirectoryEntry(rootPath, fileInfoProvider.PathTools), root));

        while (queue.Count > 0)
        {
            (DirectoryEntry info, DirTransferStream currentStream) = queue.Dequeue();

            var entriesR = fileInfoProvider.GetPathEntries(
                info.PathToEntry,
                includeHidden,
                includeOs
            );
            if (!entriesR.IsOk)
                return ValueResult<DirTransferStream>.Error(entriesR);

            foreach (FileEntryInfo file in entriesR.Value.Files)
            {
                ValueResult<Stream> streamR = fileInfoProvider.GetFileStream(file.PathToEntry);
                if (!streamR.IsOk)
                    return ValueResult<DirTransferStream>.Error(streamR);

                currentStream.Files.Add(new FileTransferStream(file.Name, streamR.Value));
            }

            foreach (DirectoryEntryInfo dir in entriesR.Value.Dirs)
            {
                DirTransferStream stream = new(dir.Name);
                queue.Enqueue((dir, stream));
                currentStream.Directories.Add(stream);
            }
        }
        return root.OkResult();
    }
}
