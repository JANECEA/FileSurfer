using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace FileSurfer.Models;

/// <summary>
/// Interacts with the program and system clipboards using <see cref="System.Windows.Forms"/>.
/// </summary>
class ClipboardManager
{
    private readonly string _newImageName;
    private readonly IFileIOHandler _fileIOHandler;
    private List<FileSystemEntry> _programClipboard = new();
    private string _copyFromDir = string.Empty;

    /// <summary>
    /// Indicates if <see cref="_programClipboard"/>'s contents are meant to be cut or copied from their original location.
    /// </summary>
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
            if (errorOccured)
                errorMessage = errorMessage?.TrimEnd(',');
            else
                errorMessage = null;

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

        foreach (FileSystemEntry entry in _programClipboard)
        {
            if (!filePaths.Contains(entry.PathToEntry))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Copies the <paramref name="filePath"/> to the system's clipboard.
    /// </summary>
    [STAThread]
    public void CopyPathToFile(string filePath) => Clipboard.SetText('\"' + filePath + '\"');

    /// <summary>
    /// Gets the contents of <see cref="_programClipboard"/>.
    /// </summary>
    /// <returns>An array of <see cref="FileSystemEntry"/>s.</returns>
    public FileSystemEntry[] GetClipboard() => _programClipboard.ToArray();

    /// <summary>
    /// Stores <paramref name="selectedFiles"/> to both <see cref="Clipboard"/> and <see cref="_programClipboard"/>.
    /// <para>
    /// Sets <see cref="IsCutOperation"/> to <see langword="true"/>.
    /// </para>
    /// </summary>
    /// <returns><see langword="true"/> if the operation was successful, otherwise <see langword="false"/>.</returns>
    public bool Cut(
        List<FileSystemEntry> selectedFiles,
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
        else
            return false;
    }

    /// <summary>
    /// Stores the selection of <see cref="FileSystemEntry"/> in <see cref="_programClipboard"/> and the system clipboard.
    /// <para>
    /// Sets <see cref="IsCutOperation"/> to <see langword="false"/>.
    /// </para>
    /// </summary>
    /// <returns><see langword="true"/> if the operation was successful, otherwise <see langword="false"/>.</returns>
    public bool Copy(
        List<FileSystemEntry> selectedFiles,
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
        else
            return false;
    }

    /// <summary>
    /// Determines if the current copy operation is occuring in the same directory.
    /// </summary>
    /// <param name="currentDir"></param>
    /// <returns><see langword="true"/> if <see cref="_copyFromDir"/> and <paramref name="currentDir"/> are equal, otherwise <see langword="false"/>.</returns>
    public bool IsDuplicateOperation(string currentDir) =>
        !IsCutOperation && _copyFromDir == currentDir && _programClipboard.Count > 0;

    /// <summary>
    /// Pastes the contents of the system clipboard into <paramref name="currentDir"/>.
    /// </summary>
    /// <returns><see langword="true"/> if the operation was successful, otherwise <see langword="false"/>.</returns>
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
            foreach (FileSystemEntry entry in _programClipboard)
            {
                bool result = entry.IsDirectory
                    ? _fileIOHandler.DeleteDir(entry.PathToEntry, out _)
                    : _fileIOHandler.DeleteFile(entry.PathToEntry, out _);

                errorOccured = !result || errorOccured;
                if (!result)
                    errorMessage += $" \"{entry.PathToEntry}\",";
            }
            errorMessage?.TrimEnd(',');
            _programClipboard.Clear();
            Clipboard.Clear();
        }
        if (!errorOccured)
            errorMessage = null;
        return !errorOccured;
    }

    /// <summary>
    /// Duplicates the files stored in <see cref="_programClipboard"/>.
    /// </summary>
    /// <returns><see langword="true"/> if the operation was successful, otherwise <see langword="false"/>.</returns>
    public bool Duplicate(string currentDir, out string[] copyNames, out string? errorMessage)
    {
        copyNames = new string[_programClipboard.Count];
        errorMessage = "Problems occured duplicating these files:";
        bool errorOccured = false;
        for (int i = 0; i < _programClipboard.Count; i++)
        {
            FileSystemEntry entry = _programClipboard[i];
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
