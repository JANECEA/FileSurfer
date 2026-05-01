using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileInformation;
using FileSurfer.Core.Services.FileOperations;
using FileSurfer.Core.Services.Shell;
using FileSurfer.Core.Services.VersionControl;

namespace Mocks;

public class MockFileSystem : IFileSystem
{
    private readonly bool _isLocal;

    public IFileInfoProvider FileInfoProvider { get; set; } = new MockFileInfoProvider();
    public IIconProvider IconProvider { get; set; } = new MockIconProvider();
    public IArchiveManager ArchiveManager { get; set; } = new MockArchiveManager();
    public IFileIoHandler FileIoHandler { get; set; } = new MockFileIoHandler();
    public IBinInteraction BinInteraction { get; set; } = new MockBinInteraction();
    public IFileProperties FileProperties { get; set; } = new MockFileProperties();
    public IShellHandler ShellHandler { get; set; } = new MockShellHandler();
    public IGitIntegration GitIntegration { get; set; } = new MockGitIntegration();

    public MockFileSystem(bool isLocal) => _isLocal = isLocal;

    public bool IsReady() => true;

    public bool IsLocal() => _isLocal;

    public string GetLabel() => "mock";

    public void Dispose() { }
}
