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
static class ArchiveManager
{
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
    /// <returns><see langword="true"/> if the operation was successful, otherwise <see langword="false"/>.</returns>
    public static bool ZipFiles(
        IEnumerable<FileSystemEntry> entries,
        string destinationDir,
        string archiveName,
        out string? errorMessage
    )
    {
        FileStream zipStream = File.OpenWrite(Path.Combine(destinationDir, archiveName));
        List<FileStream> fileStreams = new() { zipStream };
        try
        {
            using ZipArchive archive = ZipArchive.Create();

            foreach (FileSystemEntry entry in entries)
                if (entry.IsDirectory)
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
            errorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
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
    /// <returns><see langword="true"/> if the operation was successful, otherwise <see langword="false"/>.</returns>
    public static bool UnzipArchive(
        string archivePath,
        string destinationPath,
        out string? errorMessage
    )
    {
        if (!IsZipped(archivePath))
        {
            errorMessage = $"\"{archivePath}\" is not an archive.";
            return false;
        }
        try
        {
            string extractName = FileNameGenerator.GetAvailableName(
                destinationPath,
                Path.GetFileNameWithoutExtension(archivePath)
            );
            string extractTo = Path.Combine(destinationPath, extractName);

            Directory.CreateDirectory(extractTo);
            using IArchive archive = ArchiveFactory.Open(archivePath);
            foreach (IArchiveEntry file in archive.Entries.Where(entry => !entry.IsDirectory))
            {
                file.WriteToDirectory(
                    extractTo,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                );
            }
            errorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }
}
