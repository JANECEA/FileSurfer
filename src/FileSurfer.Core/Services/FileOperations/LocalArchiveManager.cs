using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
/// Handles interactions with archives using the <see cref="SharpCompress"/> package.
/// </summary>
public class LocalArchiveManager : IArchiveManager
{
    private sealed record ArchiveType(string Extension, Func<Stream, IReader>? FactoryFn);

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

    public LocalArchiveManager(IFileInfoProvider fileInfoProvider) =>
        _fileInfoProvider = fileInfoProvider;

    public bool IsArchived(string filePath) => GetArchiveExtension(filePath) is not null;

    private static ArchiveType? GetArchiveExtension(string filePath)
    {
        filePath = LocalPathTools.NormalizePath(filePath);
        foreach (ArchiveType type in SupportedFormats)
            if (filePath.EndsWith(type.Extension, LocalPathTools.Comparison))
                return type;

        return null;
    }

    [
        SuppressMessage(
            "Reliability",
            "CA2016:Forward the \'CancellationToken\' parameter to methods"
        ),
        SuppressMessage("ReSharper", "MethodSupportsCancellation"),
    ]
    private static async Task<IResult> ArchiveInternal(
        IList<IFileSystemEntry> entries,
        string destinationDir,
        string archivePath,
        List<FileStream> fileStreams,
        CancellationToken ct
    )
    {
        using ZipArchive archive = ZipArchive.Create();
        await using FileStream zipStream = File.OpenWrite(archivePath);

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
                    string relativePath = Path.GetRelativePath(destinationDir, filePath);
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

    public async Task<IResult> ArchiveEntries(
        IList<IFileSystemEntry> entries,
        string destinationDir,
        string archiveName,
        ProgressReporter reporter,
        CancellationToken ct
    )
    {
        string name = FileNameGenerator.GetAvailableName(
            _fileInfoProvider,
            destinationDir,
            archiveName + ArchiveTypeExtension
        );
        string archivePath = LocalPathTools.Combine(destinationDir, name);

        IndeterminateReporter r = new(reporter);
        r.ReportItem($"Archiving \"{name}\".");
        List<FileStream> fileStreams = new();

        try
        {
            return await ArchiveInternal(entries, destinationDir, archivePath, fileStreams, ct);
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

    private async Task<IResult> ExtractInternal(
        string archivePath,
        string destinationPath,
        ArchiveType archiveType,
        ProgressReporter reporter,
        CancellationToken ct
    )
    {
        IndeterminateReporter rep = new(reporter);
        string extractName = FileNameGenerator.GetAvailableName(
            _fileInfoProvider,
            destinationPath,
            archivePath[..^archiveType.Extension.Length]
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

    public async Task<IResult> ExtractArchive(
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

    private static IReader GetReader(Stream stream, ArchiveType archiveType) =>
        archiveType.FactoryFn is not null
            ? archiveType.FactoryFn(stream)
            : ReaderFactory.Open(stream);
}
