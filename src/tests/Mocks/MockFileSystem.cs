using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileInformation;
using FileSurfer.Core.Services.FileOperations;
using FileSurfer.Core.Services.Shell;
using FileSurfer.Core.Services.VersionControl;

namespace TestMocks;

public class MockFileSystem : IFileSystem
{
    private readonly bool _isLocal;

    public IFileInfoProvider FileInfoProvider { get; set; }
    public IIconProvider IconProvider { get; set; }
    public IArchiveManager ArchiveManager { get; set; }
    public IFileIoHandler FileIoHandler { get; set; }
    public IBinInteraction BinInteraction { get; set; }
    public IFileProperties FileProperties { get; set; }
    public IShellHandler ShellHandler { get; set; }
    public IGitIntegration GitIntegration { get; set; }

    public MockFileSystem(bool isLocal) => _isLocal = isLocal;

    public bool IsReady() => true;

    public bool IsLocal() => _isLocal;

    public string GetLabel() => "mock";

    public void Dispose() { }
}
