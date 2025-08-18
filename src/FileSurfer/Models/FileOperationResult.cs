using System.Collections.Generic;
using System.Linq;

namespace FileSurfer.Models;

public interface IFileOperationResult
{
    public bool IsOK { get; }
    public IEnumerable<string> Errors { get; }
}

public class FileOperationResult : IFileOperationResult
{
    public bool IsOK => _errors is null || _errors.Count == 0;
    public IEnumerable<string> Errors => _errors ?? Enumerable.Empty<string>();
    private List<string>? _errors;

    private FileOperationResult(string? errorMessage, List<string>? errors)
    {
        if (errorMessage is not null)
            _errors = new List<string>() { errorMessage };

        if (errors is not null)
            _errors = errors;
    }

    public static FileOperationResult Ok() => new(null, null);

    public static FileOperationResult Error(string errorMessage) => new(errorMessage, null);

    public static FileOperationResult MultipleErrors(List<string> errors) => new(null, errors);

    public static implicit operator bool(FileOperationResult result) => result.IsOK;

    public void AddError(string errorMessage)
    {
        _errors ??= new();
        _errors.Add(errorMessage);
    }

    public void AddResult(IFileOperationResult result)
    {
        IEnumerator<string> enumerator = result.Errors.GetEnumerator();
        if (!enumerator.MoveNext())
            return;

        _errors ??= new();

        do _errors.Add(enumerator.Current);
        while (enumerator.MoveNext());
    }
}
