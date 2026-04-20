using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileInformation;
using FileSurfer.Core.Services.Dialogs;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Writers;

namespace FileSurfer.Core.Services.FileOperations;

/// <summary>
/// Handles archive detection, creation, and extraction for local file-system entries using
/// <see cref="SharpCompress"/> readers and writers.
/// </summary>
public class LocalArchiveManager : IArchiveManager
{
    private sealed record ArchiveType(string Extension, Func<Stream, IReader>? FactoryFn);

    /// <summary>
    /// Gets the default extension used when creating new archives.
    /// </summary>
    public const string ArchiveTypeExtension = ".zip";
    private static readonly ExtractionOptions ExtractionOptions = new()
    {
        ExtractFullPath = true,
        Overwrite = true,
    };
    private static readonly IReadOnlyList<ArchiveType> SupportedFormats =
    [
        new(".zip", null),
        new(".rar", null),
        new(".7z", stream => SevenZipArchive.Open(stream).ExtractAllEntries()),
        new(".gzip", null),
        new(".tar.gz", null),
        new(".tar", null),
        new(".gz", null),
    ];

    private readonly IFileInfoProvider _fileInfoProvider;
    private readonly IFileIoHandler _fileIoHandler;

    /// <summary>
    /// Initializes a local archive manager with file metadata and file I/O services.
    /// </summary>
    /// <param name="fileInfoProvider">
    /// File information provider used for name availability checks and path-entry queries.
    /// </param>
    /// <param name="fileIoHandler">
    /// File I/O handler used for cleanup operations such as deleting failed archive outputs.
    /// </param>
    public LocalArchiveManager(IFileInfoProvider fileInfoProvider, IFileIoHandler fileIoHandler)
    {
        _fileInfoProvider = fileInfoProvider;
        _fileIoHandler = fileIoHandler;
    }

    private static IReader GetReader(Stream stream, ArchiveType archiveType) =>
        archiveType.FactoryFn is not null
            ? archiveType.FactoryFn(stream)
            : ReaderFactory.Open(stream);

    public bool IsArchived(string filePath) => GetArchiveExtension(filePath) is not null;

    private static ArchiveType? GetArchiveExtension(string filePath)
    {
        filePath = LocalPathTools.NormalizePath(filePath);
        foreach (ArchiveType type in SupportedFormats)
            if (filePath.EndsWith(type.Extension, LocalPathTools.Comparison))
                return type;

        return null;
    }

    private static async Task<IResult> RunArchivation(
        IList<IFileSystemEntry> entries,
        FileStream zipStream,
        List<FileStream> fileStreams,
        CancellationToken ct
    )
    {
        using ZipArchive archive = ZipArchive.Create();

        await Task.Run(() =>
        {
            foreach (IFileSystemEntry entry in entries.Where(e => e is FileEntry))
            {
                ct.ThrowIfCancellationRequested();
                FileStream fileStream = File.OpenRead(entry.PathToEntry);
                archive.AddEntry(entry.Name, fileStream);
                fileStreams.Add(fileStream);
            }

            foreach (IFileSystemEntry entry in entries.Where(e => e is DirectoryEntry))
            {
                ct.ThrowIfCancellationRequested();
                string[] allFiles = Directory.GetFiles(
                    entry.PathToEntry,
                    "*.*",
                    SearchOption.AllDirectories
                );
                foreach (string filePath in allFiles)
                {
                    string parent = LocalPathTools.GetParentDir(entry.PathToEntry);
                    string relativePath = Path.GetRelativePath(parent, filePath);
                    ct.ThrowIfCancellationRequested();
                    FileStream fileStream = File.OpenRead(filePath);
                    archive.AddEntry(relativePath, fileStream);
                    fileStreams.Add(fileStream);
                }
            }
        });

        await Task.Run(async () =>
        {
            await using CancellationTokenRegistration cr = ct.Register(zipStream.Close);
            await archive.SaveToAsync(zipStream, new WriterOptions(CompressionType.Deflate), ct);
        });
        return SimpleResult.Ok();
    }

    private static async Task<IResult> ArchiveInternal(
        IList<IFileSystemEntry> entries,
        string archivePath,
        CancellationToken ct
    )
    {
        List<FileStream> fileStreams = new();
        try
        {
            await using FileStream zipStream = File.OpenWrite(archivePath);
            return await RunArchivation(entries, zipStream, fileStreams, ct);
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
        {
            return SimpleResult.Error("Archivation has been cancelled.");
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
        finally
        {
            foreach (FileStream fileStream in fileStreams)
                fileStream.Close();
        }
    }

    public async Task<IResult> ArchiveEntriesAsync(
        IList<IFileSystemEntry> entries,
        string destinationDir,
        string archiveName,
        ProgressReporter reporter,
        CancellationToken ct
    )
    {
        string name = await FileNameGenerator.GetAvailableNameAsync(
            _fileInfoProvider,
            destinationDir,
            archiveName + ArchiveTypeExtension
        );
        string archivePath = LocalPathTools.Combine(destinationDir, name);

        IndeterminateReporter r = new(reporter);
        r.ReportItem($"Archiving \"{name}\".");

        IResult result = await ArchiveInternal(entries, archivePath, ct);
        if (!result.IsOk)
            return Result.Error(result).MergeResult(_fileIoHandler.DeleteFile(archivePath));

        return SimpleResult.Ok();
    }

    private async Task<IResult> ExtractInternal(
        string archivePath,
        string destinationPath,
        ArchiveType archiveType,
        ProgressReporter reporter,
        CancellationToken ct
    )
    {
        IndeterminateReporter rep = new(reporter);
        string archiveName = LocalPathTools.GetFileName(archivePath);
        string extractName = await FileNameGenerator.GetAvailableNameAsync(
            _fileInfoProvider,
            destinationPath,
            archiveName[..^archiveType.Extension.Length]
        );
        string extractTo = Path.Combine(destinationPath, extractName);

        Directory.CreateDirectory(extractTo);
        await using Stream stream = File.OpenRead(archivePath);
        using IReader reader = GetReader(stream, archiveType);

        while (await reader.MoveToNextEntryAsync(ct))
        {
            ct.ThrowIfCancellationRequested();
            if (reader.Entry.Key is string key)
                rep.ReportItem($"Extracting: \"{LocalPathTools.GetFileName(key)}\"");

            await reader
                .WriteEntryToDirectoryAsync(extractTo, ExtractionOptions, ct)
                .ConfigureAwait(false);
        }
        return SimpleResult.Ok();
    }

    public async Task<IResult> ExtractArchiveAsync(
        string archivePath,
        string destinationPath,
        ProgressReporter reporter,
        CancellationToken ct
    )
    {
        if (GetArchiveExtension(archivePath) is not ArchiveType archiveType)
            return SimpleResult.Error($"\"{archivePath}\" is not an archive.");

        try
        {
            return await ExtractInternal(archivePath, destinationPath, archiveType, reporter, ct);
        }
        catch (OperationCanceledException)
        {
            return SimpleResult.Error("Extraction has been canceled.");
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }
}
