using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileInformation;
using SharpCompress.Archives;
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

    public bool IsZipped(string filePath) => GetZipExtension(filePath) is not null;

    private static ArchiveType? GetZipExtension(string filePath)
    {
        filePath = LocalPathTools.NormalizePath(filePath);
        foreach (ArchiveType type in SupportedFormats)
            if (filePath.EndsWith(type.Extension, LocalPathTools.Comparison))
                return type;

        return null;
    }

    private static async Task<IResult> ZipFilesInternal(
        IList<IFileSystemEntry> entries,
        string destinationDir,
        string archiveName,
        List<FileStream> fileStreams,
        CancellationToken ct
    )
    {
        using ZipArchive archive = ZipArchive.Create();
        FileStream zipStream = File.OpenWrite(Path.Combine(destinationDir, archiveName));

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
            archive.AddAllFromDirectory(entry.PathToEntry);
        }

        await archive.SaveToAsync(zipStream, new WriterOptions(CompressionType.Deflate), ct);
        return SimpleResult.Ok();
    }

    public async Task<IResult> ZipFiles(
        IList<IFileSystemEntry> entries,
        string destinationDir,
        string archiveName,
        CancellationToken ct
    )
    {
        string name = FileNameGenerator.GetAvailableName(
            _fileInfoProvider,
            destinationDir,
            archiveName + ArchiveTypeExtension
        );
        List<FileStream> fileStreams = new();
        try
        {
            return await Task.Run(
                async () => await ZipFilesInternal(entries, destinationDir, name, fileStreams, ct),
                ct
            );
        }
        catch (OperationCanceledException)
        {
            return SimpleResult.Error("Compression has been cancelled.");
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

    private async Task<IResult> UnzipArchiveInternal(
        string archivePath,
        string destinationPath,
        ArchiveType archiveType,
        CancellationToken ct
    )
    {
        string extractName = FileNameGenerator.GetAvailableName(
            _fileInfoProvider,
            destinationPath,
            archivePath[..^archiveType.Extension.Length]
        );
        string extractTo = Path.Combine(destinationPath, extractName);

        Directory.CreateDirectory(extractTo);
        await using Stream stream = File.OpenRead(archivePath);
        using IReader reader = GetReader(stream, archiveType);

        await reader.WriteAllToDirectoryAsync(extractTo, ExtractionOptions, ct);
        return SimpleResult.Ok();
    }

    public async Task<IResult> UnzipArchive(
        string archivePath,
        string destinationPath,
        CancellationToken ct
    )
    {
        if (GetZipExtension(archivePath) is not ArchiveType archiveType)
            return SimpleResult.Error($"\"{archivePath}\" is not an archive.");

        try
        {
            return await UnzipArchiveInternal(archivePath, destinationPath, archiveType, ct);
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
