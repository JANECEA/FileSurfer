using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using FileSurfer.Models.FileInformation;
using FileSurfer.ViewModels;

namespace FileSurfer.Models.FileOperations;

/// <summary>
/// Interacts with the program and system clipboards using <see cref="System.Windows.Forms"/>.
/// </summary>
public class ClipboardManager : IClipboardManager
{
    private readonly string _newImageName;
    private readonly IFileIOHandler _fileIOHandler;
    private List<FileSystemEntryViewModel> _programClipboard = new();
    private string _copyFromDir = string.Empty;

    public bool IsCutOperation { get; private set; }

    public ClipboardManager(IFileIOHandler fileIOHandler, string newImageName)
    {
        _newImageName = newImageName;
        _fileIOHandler = fileIOHandler;
    }

    [STAThread]
    private static void ClearClipboard() => Clipboard.Clear();

    [STAThread]
    private static bool CopyToOSClipboard(string[] paths, out string? errorMessage)
    {
        try
        {
            StringCollection fileCollection = new();
            fileCollection.AddRange(paths);
            Clipboard.SetFileDropList(fileCollection);
            errorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            Clipboard.Clear();
            errorMessage = ex.Message;
            return false;
        }
    }

    [STAThread]
    private bool PasteFromOSClipboard(string destinationPath, out string? errorMessage)
    {
        errorMessage = null;
        if (FileSurferSettings.AllowImagePastingFromClipboard && Clipboard.ContainsImage())
        {
            SaveImageToPath(destinationPath, out errorMessage);
            return false;
        }
        if (!Clipboard.ContainsFileDropList())
            return true;

        try
        {
            StringCollection fileCollection = Clipboard.GetFileDropList();
            errorMessage = "Problems occured pasting these files:";
            bool errorOccured = false;
            foreach (string? path in fileCollection)
            {
                if (path is null)
                    throw new ArgumentNullException(path);

                bool result =
                    Directory.Exists(path)
                        && _fileIOHandler.CopyDirTo(path, destinationPath, out errorMessage)
                    || File.Exists(path)
                        && _fileIOHandler.CopyFileTo(path, destinationPath, out errorMessage);

                errorOccured = !result || errorOccured;
                if (!result)
                    errorMessage += $" \"{path}\",";
            }
            errorMessage = errorOccured ? errorMessage?.TrimEnd(',') : null;
            return !errorOccured;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    [STAThread]
    private void SaveImageToPath(string destinationPath, out string? errorMessage)
    {
        errorMessage = null;
        if (Clipboard.GetImage() is not Image image)
            return;

        string imgName = FileNameGenerator.GetAvailableName(destinationPath, _newImageName);
        try
        {
            image.Save(Path.Combine(destinationPath, imgName), ImageFormat.Png);
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
        }
        image.Dispose();
    }

    [STAThread]
    private bool CompareClipboards()
    {
        if (Clipboard.GetFileDropList() is not StringCollection filePaths)
            return false;

        foreach (FileSystemEntryViewModel entry in _programClipboard)
        {
            if (!filePaths.Contains(entry.PathToEntry))
                return false;
        }
        return true;
    }

    [STAThread]
    public void CopyPathToFile(string filePath) => Clipboard.SetText('\"' + filePath + '\"');

    public FileSystemEntryViewModel[] GetClipboard() => _programClipboard.ToArray();

    public bool Cut(
        List<FileSystemEntryViewModel> selectedFiles,
        string currentDir,
        out string? errorMessage
    )
    {
        if (
            CopyToOSClipboard(
                selectedFiles.Select(entry => entry.PathToEntry).ToArray(),
                out errorMessage
            )
        )
        {
            _copyFromDir = currentDir;
            IsCutOperation = true;
            _programClipboard = selectedFiles;
            return true;
        }
        return false;
    }

    public bool Copy(
        List<FileSystemEntryViewModel> selectedFiles,
        string currentDir,
        out string? errorMessage
    )
    {
        if (
            CopyToOSClipboard(
                selectedFiles.Select(entry => entry.PathToEntry).ToArray(),
                out errorMessage
            )
        )
        {
            _copyFromDir = currentDir;
            IsCutOperation = false;
            _programClipboard = selectedFiles;
            return true;
        }
        return false;
    }

    public bool IsDuplicateOperation(string currentDir) =>
        !IsCutOperation && _copyFromDir == currentDir && _programClipboard.Count > 0;

    public bool Paste(string currentDir, out string? errorMessage)
    {
        if (!PasteFromOSClipboard(currentDir, out errorMessage))
        {
            _programClipboard.Clear();
            return false;
        }
        errorMessage = "Problems occured moving these files:";

        IsCutOperation = IsCutOperation && CompareClipboards();
        bool errorOccured = false;
        if (IsCutOperation)
        {
            foreach (FileSystemEntryViewModel entry in _programClipboard)
            {
                bool result = entry.IsDirectory
                    ? _fileIOHandler.DeleteDir(entry.PathToEntry, out _)
                    : _fileIOHandler.DeleteFile(entry.PathToEntry, out _);

                errorOccured = !result || errorOccured;
                if (!result)
                    errorMessage += $" \"{entry.PathToEntry}\",";
            }
            errorMessage = errorMessage.TrimEnd(',');
            _programClipboard.Clear();
            Clipboard.Clear();
        }
        if (!errorOccured)
            errorMessage = null;
        return !errorOccured;
    }

    public bool Duplicate(string currentDir, out string[] copyNames, out string? errorMessage)
    {
        copyNames = new string[_programClipboard.Count];
        errorMessage = "Problems occured duplicating these files:";
        bool errorOccured = false;
        for (int i = 0; i < _programClipboard.Count; i++)
        {
            FileSystemEntryViewModel entry = _programClipboard[i];
            copyNames[i] = FileNameGenerator.GetCopyName(currentDir, entry);

            bool result = entry.IsDirectory
                ? _fileIOHandler.DuplicateDir(entry.PathToEntry, copyNames[i], out _)
                : _fileIOHandler.DuplicateFile(entry.PathToEntry, copyNames[i], out _);

            errorOccured = !result || errorOccured;
            if (!result)
                errorMessage += $" \"{entry.PathToEntry}\",";
        }
        if (errorOccured)
        {
            ClearClipboard();
            _programClipboard.Clear();
        }
        else
            errorMessage = null;
        return !errorOccured;
    }
}
