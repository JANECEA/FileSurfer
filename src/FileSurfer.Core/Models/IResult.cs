using System;
using System.Collections.Generic;
using System.Linq;

// ReSharper disable UnusedMember.Global

namespace FileSurfer.Core.Models;

/// <summary>
/// Represents the result of an operation, indicating success or failure,
/// along with error messages.
/// </summary>
public interface IResult
{
    /// <summary>
    /// Value indicating whether the operation was successful.
    /// </summary>
    public bool IsOk { get; }

    /// <summary>
    /// Collection of error messages describing why the operation failed.
    /// Empty if the operation succeeded.
    /// </summary>
    public IEnumerable<string> Errors { get; }
}

/// <summary>
/// An immutable, lightweight, and memory efficient implementation of <see cref="IResult"/>
/// that supports at most one error message.
/// </summary>
public class SimpleResult : IResult
{
    private static readonly IEnumerable<string> EmptyEnumerable = Enumerable.Empty<string>();
    private static readonly SimpleResult OkResult = new(true, null);
    private static readonly SimpleResult ErrorEmptyResult = new(false, null);

    public bool IsOk => _errors is null;
    public IEnumerable<string> Errors => _errors ?? EmptyEnumerable;
    private readonly IEnumerable<string>? _errors;

    protected SimpleResult(bool isOk, string? errorMessage)
    {
        if (!isOk)
            _errors = errorMessage is null ? EmptyEnumerable : GetEnumerable(errorMessage);
    }

    private static IEnumerable<string> GetEnumerable(string errorMessage)
    {
        yield return errorMessage;
    }

    public static SimpleResult Ok() => OkResult;

    public static SimpleResult Error() => ErrorEmptyResult;

    public static SimpleResult Error(string errorMessage) => new(false, errorMessage);
}

/// <summary>
/// An immutable, lightweight, and memory efficient implementation of <see cref="IResult"/>
/// that extends <see cref="SimpleResult"/> and includes a value if <c>IsOk</c> is <see langword="true"/>
/// <br/>
/// Supports at most one error message.
/// </summary>
public sealed class ValueResult<T> : SimpleResult
{
    private static readonly ValueResult<T> ErrorEmptyResult = new(default, false, null);

    private readonly T? _value = default;
    public T Value => IsOk ? _value! : throw new InvalidOperationException();

    private ValueResult(T? value, bool isOk, string? errorMessage)
        : base(isOk, errorMessage) => _value = value;

    public static ValueResult<T> Ok(T value) => new(value, true, null);

    public static new ValueResult<T> Error() => ErrorEmptyResult;

    public static new ValueResult<T> Error(string errorMessage) =>
        new(default, false, errorMessage);
}

/// <summary>
/// A flexible implementation of <see cref="IResult"/> that supports
/// multiple error messages and can be updated after creation.
/// </summary>
public sealed class Result : IResult
{
    private static readonly IEnumerable<string> EmptyEnumerable = Enumerable.Empty<string>();

    public bool IsOk => _errors is null || _errors.Count == 0;
    public IEnumerable<string> Errors => _errors ?? EmptyEnumerable;
    private List<string>? _errors;

    private Result(string? errorMessage, List<string>? errors)
    {
        if (errorMessage is not null)
            _errors = new List<string> { errorMessage };

        if (errors is not null)
            _errors = errors;
    }

    public static Result Ok() => new(null, null);

    public static Result Error(string errorMessage) => new(errorMessage, null);

    public static Result MultipleErrors(IEnumerable<string> errors) => new(null, errors.ToList());

    public void AddError(string errorMessage)
    {
        _errors ??= new List<string>();
        _errors.Add(errorMessage);
    }

    public Result MergeResult(IResult result)
    {
        using IEnumerator<string> enumerator = result.Errors.GetEnumerator();
        if (!enumerator.MoveNext())
            return this;

        _errors ??= new List<string>();

        do _errors.Add(enumerator.Current);
        while (enumerator.MoveNext());

        return this;
    }
}
