using System;
using System.IO;
using System.Linq;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Writers;

namespace FileSurfer.Models;

static class ArchiveManager
{
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

    public static bool ZipFiles(
        string[] filePaths,
        string destinationPath,
        out string? errorMessage
    )
    {
        try
        {
            using ZipArchive archive = ZipArchive.Create();
            using FileStream zipStream = File.OpenWrite(destinationPath);

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

    public static bool UnzipArchive(
        string archivePath,
        string extractPath,
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
            Directory.CreateDirectory(Path.Combine(extractPath, extractName));
            using IArchive archive = ArchiveFactory.Open(archivePath);
            foreach (IArchiveEntry entry in archive.Entries.Where(entry => !entry.IsDirectory))
            {
                entry.WriteToDirectory(
                    extractPath,
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
