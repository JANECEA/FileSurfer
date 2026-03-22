using System;
using System.Collections.Generic;
using System.IO;
using FileSurfer.Core.Extensions;
using FileSurfer.Core.Models.FileInformation;

namespace FileSurfer.Core.Models;

public class FileTransferStream : IDisposable
{
    private readonly Func<ValueResult<Stream>> _getStream;

    private Lazy<ValueResult<Stream>> _fileStream;
    public ValueResult<Stream> FileStream => _fileStream.Value;

    public string Name { get; }

    public FileTransferStream(string name, Func<ValueResult<Stream>> getStream)
    {
        _getStream = getStream;
        _fileStream = new Lazy<ValueResult<Stream>>(getStream);
        Name = name;
    }

    public void Dispose()
    {
        if (_fileStream.IsValueCreated)
        {
            _fileStream.Value.Value.Dispose();
            _fileStream = new Lazy<ValueResult<Stream>>(_getStream);
        }
    }
}

public class DirTransferStream : IDisposable
{
    public List<DirTransferStream> Directories { get; }

    public List<FileTransferStream> Files { get; }

    public string Name { get; }

    public DirTransferStream(
        string name,
        List<DirTransferStream> directories,
        List<FileTransferStream> files
    )
    {
        Directories = directories;
        Files = files;
        Name = name;
    }

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
        Queue<(DirectoryEntry, DirTransferStream)> queue = new();
        queue.Enqueue((new DirectoryEntry(rootPath, fileInfoProvider.PathTools), root));

        while (queue.Count > 0)
        {
            (DirectoryEntry info, DirTransferStream currentStream) = queue.Dequeue();

            var dirR = fileInfoProvider.GetPathDirs(info.PathToEntry, includeHidden, includeOs);
            var fileR = fileInfoProvider.GetPathFiles(info.PathToEntry, includeHidden, includeOs);
            if (ResultExtensions.FirstError(dirR, fileR) is IResult currentResult)
                return ValueResult<DirTransferStream>.Error(currentResult);

            foreach (FileEntryInfo file in fileR.Value)
                currentStream.Files.Add(
                    new FileTransferStream(
                        file.Name,
                        () => fileInfoProvider.GetFileStream(file.PathToEntry)
                    )
                );

            foreach (DirectoryEntryInfo dir in dirR.Value)
            {
                DirTransferStream stream = new(dir.Name);
                queue.Enqueue((dir, stream));
                currentStream.Directories.Add(stream);
            }
        }

        return root.OkResult();
    }
}
