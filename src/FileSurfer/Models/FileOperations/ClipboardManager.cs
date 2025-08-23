using System;
using System.Collections.Specialized;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using FileSurfer.Models.FileInformation;

namespace FileSurfer.Models.FileOperations;

/// <summary>
/// Interacts with the program and system clipboards using <see cref="System.Windows.Forms"/>.
/// </summary>
public class ClipboardManager : IClipboardManager
{
    private readonly string _newImageName;
    private readonly IFileIOHandler _fileIOHandler;
    private IFileSystemEntry[] _programClipboard = Array.Empty<IFileSystemEntry>();
    private string _copyFromDir = string.Empty;

    public bool IsCutOperation { get; private set; }

    public ClipboardManager(IFileIOHandler fileIOHandler, string newImageName)
    {
        _newImageName = newImageName + ".png";
        _fileIOHandler = fileIOHandler;
    }

    [STAThread]
    private static void ClearClipboard() => Clipboard.Clear();

    [STAThread]
    private static SimpleResult CopyToOSClipboard(string[] paths)
    {
        try
        {
            StringCollection fileCollection = new();
            fileCollection.AddRange(paths);
            Clipboard.SetFileDropList(fileCollection);
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            Clipboard.Clear();
            return SimpleResult.Error(ex.Message);
        }
    }

    [STAThread]
    private SimpleResult SaveImageToPath(string destinationPath)
    {
        if (Clipboard.GetImage() is not Image image)
            return SimpleResult.Error("There is no image in the system clipboard");

        string imgName = FileNameGenerator.GetAvailableName(destinationPath, _newImageName);
        try
        {
            image.Save(Path.Combine(destinationPath, imgName), ImageFormat.Png);
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
        finally
        {
            image.Dispose();
        }
    }

    [STAThread]
    private bool CompareClipboards()
    {
        if (Clipboard.GetFileDropList() is not StringCollection filePaths)
            return false;

        foreach (IFileSystemEntry entry in _programClipboard)
            if (!filePaths.Contains(entry.PathToEntry))
                return false;

        return true;
    }

    [STAThread]
    public void CopyPathToFile(string filePath) => Clipboard.SetText('\"' + filePath + '\"');

    public IFileSystemEntry[] GetClipboard() => _programClipboard.ToArray();

    public IResult Cut(IFileSystemEntry[] selectedFiles, string currentDir)
    {
        SimpleResult result = CopyToOSClipboard(
            selectedFiles.Select(entry => entry.PathToEntry).ToArray()
        );
        if (result.IsOk)
        {
            _copyFromDir = currentDir;
            IsCutOperation = true;
            _programClipboard = selectedFiles;
        }
        return result;
    }

    public IResult Copy(IFileSystemEntry[] selectedFiles, string currentDir)
    {
        SimpleResult result = CopyToOSClipboard(
            selectedFiles.Select(entry => entry.PathToEntry).ToArray()
        );
        if (result.IsOk)
        {
            _copyFromDir = currentDir;
            IsCutOperation = false;
            _programClipboard = selectedFiles;
        }
        return result;
    }

    public bool IsDuplicateOperation(string currentDir) =>
        !IsCutOperation && _copyFromDir == currentDir && _programClipboard.Length > 0;

    [STAThread]
    private IResult PasteFromOSClipboard(string destinationPath)
    {
        if (!Clipboard.ContainsFileDropList())
            return SimpleResult.Ok();

        try
        {
            StringCollection fileCollection = Clipboard.GetFileDropList();
            Result result = Result.Ok();
            foreach (string? path in fileCollection)
            {
                if (path is null)
                    throw new ArgumentNullException(path);

                if (Directory.Exists(path))
                    result.MergeResult(
                        IsCutOperation
                            ? _fileIOHandler.MoveDirTo(path, destinationPath)
                            : _fileIOHandler.CopyDirTo(path, destinationPath)
                    );
                else if (File.Exists(path))
                    result.MergeResult(
                        IsCutOperation
                            ? _fileIOHandler.MoveFileTo(path, destinationPath)
                            : _fileIOHandler.CopyFileTo(path, destinationPath)
                    );
            }
            return result;
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }

    public IResult Paste(string currentDir)
    {
        if (FileSurferSettings.AllowImagePastingFromClipboard && Clipboard.ContainsImage())
        {
            SaveImageToPath(currentDir);
            return SimpleResult.Error();
        }

        IsCutOperation = IsCutOperation && CompareClipboards();
        IResult result = PasteFromOSClipboard(currentDir);
        if (IsCutOperation)
        {
            _programClipboard = Array.Empty<IFileSystemEntry>();
            Clipboard.Clear();
        }

        if (!result.IsOk)
            _programClipboard = Array.Empty<IFileSystemEntry>();

        return result;
    }

    public IResult Duplicate(string currentDir, out string[] copyNames)
    {
        copyNames = new string[_programClipboard.Length];
        Result result = Result.Ok();
        for (int i = 0; i < _programClipboard.Length; i++)
        {
            IFileSystemEntry entry = _programClipboard[i];
            copyNames[i] = FileNameGenerator.GetCopyName(currentDir, entry);

            result.MergeResult(
                entry is DirectoryEntry
                    ? _fileIOHandler.DuplicateDir(entry.PathToEntry, copyNames[i])
                    : _fileIOHandler.DuplicateFile(entry.PathToEntry, copyNames[i])
            );
        }
        if (!result.IsOk)
        {
            ClearClipboard();
            _programClipboard = Array.Empty<IFileSystemEntry>();
        }
        return result;
    }
}
