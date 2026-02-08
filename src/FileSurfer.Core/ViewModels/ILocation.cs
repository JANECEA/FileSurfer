using FileSurfer.Core.Models;

namespace FileSurfer.Core.ViewModels;

public interface ILocation
{
    public string Identifier { get; }
}

public sealed class ThisPcLocation : ILocation
{
    public static readonly ThisPcLocation Instance = new();

    public string Identifier => FileSurferSettings.ThisPcLabel;

    private ThisPcLocation() { }
}

public sealed class LocalPathLocation : ILocation
{
    public string Identifier { get; }

    public LocalPathLocation(string dirPath) => Identifier = PathTools.NormalizePath(dirPath);
}
