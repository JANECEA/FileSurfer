using System;
using System.Collections.Generic;
using System.IO;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileInformation;
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
    public const string ArchiveTypeExtension = ".zip";

    private readonly IFileInfoProvider _fileInfoProvider;

    public LocalArchiveManager(IFileInfoProvider fileInfoProvider) =>
        _fileInfoProvider = fileInfoProvider;

    private sealed record ArchiveType(string Extension, Func<Stream, IReader>? FactoryFn);

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

    public bool IsZipped(string filePath) => GetZipExtension(filePath) is not null;

    private static ArchiveType? GetZipExtension(string filePath)
    {
        filePath = PathTools.NormalizeLocalPath(filePath);
        foreach (ArchiveType type in SupportedFormats)
            if (filePath.EndsWith(type.Extension, PathTools.Comparison))
                return type;

        return null;
    }

    public IResult ZipFiles(
        IEnumerable<IFileSystemEntry> entries,
        string destinationDir,
        string archiveName
    )
    {
        FileStream zipStream = File.OpenWrite(Path.Combine(destinationDir, archiveName));
        List<FileStream> fileStreams = new() { zipStream };
        try
        {
            using ZipArchive archive = ZipArchive.Create();

            foreach (IFileSystemEntry entry in entries)
                if (entry is DirectoryEntry)
                {
                    string[] allFiles = Directory.GetFiles(
                        entry.PathToEntry,
                        "*.*",
                        SearchOption.AllDirectories
                    );
                    foreach (string filePath in allFiles)
                    {
                        string relativePath = Path.GetRelativePath(destinationDir, filePath);
                        FileStream fileStream = File.OpenRead(filePath);
                        archive.AddEntry(relativePath, fileStream);
                        fileStreams.Add(fileStream);
                    }
                }
                else
                {
                    FileStream fileStream = File.OpenRead(entry.PathToEntry);
                    archive.AddEntry(entry.Name, fileStream);
                    fileStreams.Add(fileStream);
                }

            archive.SaveTo(zipStream, new WriterOptions(CompressionType.Deflate));
            return SimpleResult.Ok();
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

    public IResult UnzipArchive(string archivePath, string destinationPath)
    {
        if (GetZipExtension(archivePath) is not ArchiveType archiveType)
            return SimpleResult.Error($"\"{archivePath}\" is not an archive.");

        try
        {
            string extractName = FileNameGenerator.GetAvailableName(
                _fileInfoProvider,
                destinationPath,
                archivePath[..^archiveType.Extension.Length]
            );
            string extractTo = Path.Combine(destinationPath, extractName);

            Directory.CreateDirectory(extractTo);
            using Stream stream = File.OpenRead(archivePath);
            using IReader reader = GetReader(stream, archiveType);

            reader.WriteAllToDirectory(
                extractTo,
                new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
            );
            return SimpleResult.Ok();
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
