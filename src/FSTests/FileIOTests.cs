using FileSurfer.Windows.Models.FileInformation;
using FileSurfer.Windows.Services.FileOperations;

namespace FSTests;

public class FileIoTests
{
    private readonly WindowsFileIoHandler _fileIoHandler = new(new WindowsFileInfoProvider(), 100);

    [Fact]
    public void Renaming_File_To_Different_Capital_Letters_Works()
    {
        // Arrange
        string parentDir = Path.GetTempPath();
        string fileName = "FileSurferRandomTestName.Random";
        string originalPath = Path.Combine(parentDir, fileName);

        File.WriteAllText(originalPath, "test content");

        string newFileName = fileName.ToUpperInvariant();
        string newPath = Path.Combine(parentDir, newFileName);

        try
        {
            // Act
            _fileIoHandler.RenameFileAt(originalPath, newFileName);

            // Assert
            string[] allFiles = Directory.GetFiles(parentDir);
            Assert.Contains(newPath, allFiles);
            Assert.DoesNotContain(originalPath, allFiles);
        }
        finally
        {
            // Cleanup
            if (File.Exists(originalPath))
                File.Delete(originalPath);
            if (File.Exists(newPath))
                File.Delete(newPath);
        }
    }

    [Fact]
    public void Renaming_Dir_To_Different_Capital_Letters_Works()
    {
        // Arrange
        string parentDir = Path.GetTempPath();
        string dirName = "FileSurferRandomTestName.Random";
        string originalPath = Path.Combine(parentDir, dirName);

        Directory.CreateDirectory(originalPath);

        string newDirName = dirName.ToUpperInvariant();
        string newPath = Path.Combine(parentDir, newDirName);

        try
        {
            // Act
            _fileIoHandler.RenameDirAt(originalPath, newDirName);

            // Assert
            string[] allDirs = Directory.GetDirectories(parentDir);
            Assert.Contains(newPath, allDirs);
            Assert.DoesNotContain(originalPath, allDirs);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(originalPath))
                Directory.Delete(originalPath);
            if (Directory.Exists(newPath))
                Directory.Delete(newPath);
        }
    }
}
