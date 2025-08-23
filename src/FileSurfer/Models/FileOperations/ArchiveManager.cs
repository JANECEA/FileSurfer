using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FileSurfer.Models.FileInformation;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Writers;

namespace FileSurfer.Models.FileOperations;

/// <summary>
/// Handles interactions with archives using the <see cref="SharpCompress"/> package.
/// </summary>
internal static class ArchiveManager
{
    public const string ArchiveTypeExtension = ".zip";

    /// <summary>
    /// Determines if the file is an archive in the context of <see cref="FileSurfer"/>.
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <returns><see langword="true"/> if the file has one of the supported extensions, otherwise <see langword="false"/>.</returns>
    public static bool IsZipped(string filePath) =>
        Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".zip" => true,
            ".rar" => true,
            ".7z" => true,
            ".gzip" => true,
            ".tar" => true,
            _ => false,
        };

    /// <summary>
    /// Compresses specified file paths into a new archive.
    /// </summary>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public static IResult ZipFiles(
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

    /// <summary>
    /// Extracts an archive, overwriting the already existing files.
    /// </summary>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public static IResult UnzipArchive(string archivePath, string destinationPath)
    {
        if (!IsZipped(archivePath))
            return SimpleResult.Error($"\"{archivePath}\" is not an archive.");

        try
        {
            string extractName = FileNameGenerator.GetAvailableName(
                destinationPath,
                Path.GetFileNameWithoutExtension(archivePath)
            );
            string extractTo = Path.Combine(destinationPath, extractName);

            Directory.CreateDirectory(extractTo);
            using IArchive archive = ArchiveFactory.Open(archivePath);
            ExtractionOptions extractionOptions =
                new() { ExtractFullPath = true, Overwrite = true };

            foreach (IArchiveEntry file in archive.Entries.Where(entry => !entry.IsDirectory))
                file.WriteToDirectory(extractTo, extractionOptions);

            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }
}
