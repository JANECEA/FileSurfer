using Avalonia.Media.Imaging;
using System.Drawing.Imaging;
using System.IO;
using System.Drawing;
using Bitmap = Avalonia.Media.Imaging.Bitmap;

namespace FileSurfer;

public abstract class FileSystemEntry(IFileOperationsHandler fileOpsHandler, string path)
{
    protected readonly IFileOperationsHandler _fileOpsHandler = fileOpsHandler;
    public readonly string PathToEntry = path;
    private readonly string _name = Path.GetFileName(path);
    public string Name => _name;

    public abstract bool OnDoubleClick(out string? errorMessage);

    public Bitmap? GetIcon()
    {
        return null;    
    }
}

class FileEntry(IFileOperationsHandler fileOpsHandler, string path) : FileSystemEntry(fileOpsHandler, path)
{
    public override bool OnDoubleClick(out string? errorMessage) => 
        _fileOpsHandler.OpenFile(PathToEntry, out errorMessage);
}

class DirectoryEntry(IFileOperationsHandler fileOpsHandler, string path) : FileSystemEntry(fileOpsHandler, path)
{
    public override bool OnDoubleClick(out string? errorMessage) => 
        _fileOpsHandler.OpenFile(PathToEntry, out errorMessage);
}