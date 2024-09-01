using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Writers;

namespace FileSurfer.Models;

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
    /// <returns><see langword="true"/> if the operation was succesfull, otherwise <see langword="false"/>.</returns>
    public static bool ZipFiles(
        IEnumerable<string> filePaths,
        string destinationPath,
        string archiveName,
        out string? errorMessage
    )
    {
        try
        {
            using ZipArchive archive = ZipArchive.Create();
            using FileStream zipStream = File.OpenWrite(Path.Combine(destinationPath, archiveName));

            foreach (string filePath in filePaths)
                archive.AddEntry(Path.GetFileName(filePath), File.OpenRead(filePath));

            archive.SaveTo(zipStream, new WriterOptions(CompressionType.Deflate));
            errorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Extraxts an archive, overwriting the already existing files.
    /// </summary>
    /// <returns><see langword="true"/> if the operation was succesfull, otherwise <see langword="false"/>.</returns>
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
            string extractName = Path.GetFileNameWithoutExtension(archivePath);
            string extractTo = Path.Combine(destinationPath, extractName);
            Directory.CreateDirectory(extractTo);
            using IArchive archive = ArchiveFactory.Open(archivePath);
            foreach (IArchiveEntry entry in archive.Entries.Where(entry => !entry.IsDirectory))
            {
                entry.WriteToDirectory(
                    extractTo,
                    new ExtractionOptions() { ExtractFullPath = true, Overwrite = true }
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
