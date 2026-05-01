using FileSurfer.Core.Models;

namespace Mocks;

public static class MockHelper
{
    public static DirectoryEntryInfo Dir(string path, DateTime utc) =>
        new(path, Path.GetFileName(path), utc.ToLocalTime(), utc);

    public static FileEntryInfo File(string path, long sizeB, DateTime utc) =>
        new(path, Path.GetFileName(path), Path.GetExtension(path), sizeB, utc.ToLocalTime(), utc);
}
