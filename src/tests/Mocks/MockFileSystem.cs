using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileInformation;
using FileSurfer.Core.Services.FileOperations;
using FileSurfer.Core.Services.Shell;
using FileSurfer.Core.Services.VersionControl;

namespace Mocks;

public struct MethodCall(string MethodName, params object[] Args);

public interface IServiceMock
{
    public List<MethodCall> Calls { get; }
}

public class MockFileSystem : IFileSystem
{
    public bool Ready { get; set; } = true;
    public bool Local { get; set; } = true;
    public string Label { get; set; } = "mock";

    public IFileInfoProvider FileInfoProvider { get; set; } = new MockFileInfoProvider();
    public IIconProvider IconProvider { get; set; } = new MockIconProvider();
    public IArchiveManager ArchiveManager { get; set; } = new MockArchiveManager();
    public IFileIoHandler FileIoHandler { get; set; } = new MockFileIoHandler();
    public IBinInteraction BinInteraction { get; set; } = new MockBinInteraction();
    public IFileProperties FileProperties { get; set; } = new MockFileProperties();
    public IShellHandler ShellHandler { get; set; } = new MockShellHandler();
    public IGitIntegration GitIntegration { get; set; } = new MockGitIntegration();

    public bool IsReady() => Ready;

    public bool IsLocal() => Local;

    public string GetLabel() => Label;

    public void Dispose() { }
}
