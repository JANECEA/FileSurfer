using FileSurfer.Core.Models;

namespace FileSurfer.Core.Extensions;

public static class FileSystemExtensions
{
    public static Location GetLocation(this IFileSystem fileSystem, string path) =>
        new(fileSystem, path);
}

public static class ResultExtensions
{
    /// <summary>
    /// Turns value into an Ok <see cref="ValueResult{T}"/>
    /// </summary>
    public static ValueResult<T> OkResult<T>(this T value) => ValueResult<T>.Ok(value);

    /// <summary>
    /// Return first failed result
    /// </summary>
    public static IResult? FirstError(params IResult[] results)
    {
        foreach (IResult result in results)
            if (!result.IsOk)
                return result;

        return null;
    }
}
