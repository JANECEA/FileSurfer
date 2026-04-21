using System;
using System.Collections.Generic;
using System.Linq;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedMethodReturnValue.Global
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
public sealed class SimpleResult : IResult
{
    private static readonly IEnumerable<string> EmptyEnumerable = Enumerable.Empty<string>();
    private static readonly SimpleResult OkResult = new(true, null);
    private static readonly SimpleResult ErrorEmptyResult = new(false, null);

    public bool IsOk => _errors is null;
    public IEnumerable<string> Errors => _errors ?? EmptyEnumerable;
    private readonly IEnumerable<string>? _errors;

    private SimpleResult(bool isOk, string? errorMessage)
    {
        if (!isOk)
            _errors = errorMessage is null ? EmptyEnumerable : GetEnumerable(errorMessage);
    }

    private static IEnumerable<string> GetEnumerable(string errorMessage)
    {
        yield return errorMessage;
    }

    /// <summary>
    /// Gets a cached successful result with no errors.
    /// </summary>
    /// <returns>
    /// Successful <see cref="SimpleResult"/> instance.
    /// </returns>
    public static SimpleResult Ok() => OkResult;

    /// <summary>
    /// Gets a cached failed result with no error message.
    /// </summary>
    /// <returns>
    /// Failed <see cref="SimpleResult"/> instance without error details.
    /// </returns>
    public static SimpleResult Error() => ErrorEmptyResult;

    /// <summary>
    /// Creates a failed result with a single error message.
    /// </summary>
    /// <param name="errorMessage">
    /// Error message describing the failure.
    /// </param>
    /// <returns>
    /// Failed <see cref="SimpleResult"/> containing the provided error message.
    /// </returns>
    public static SimpleResult Error(string errorMessage) => new(false, errorMessage);
}

/// <summary>
/// An immutable, lightweight, and memory efficient implementation of <see cref="IResult"/>
/// that extends <see cref="SimpleResult"/> and includes a value if <c>IsOk</c> is <see langword="true"/>
/// <br/>
/// Supports at most one error message.
/// </summary>
public sealed class ValueResult<T> : IResult
{
    private static readonly ValueResult<T> ErrorEmptyResult = new(SimpleResult.Error(), default);

    private readonly IResult _internalResult;

    public bool IsOk => _internalResult.IsOk;
    public IEnumerable<string> Errors => _internalResult.Errors;

    /// <summary>
    /// Gets the result value when <see cref="IsOk"/> is <see langword="true"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the result is not successful.
    /// </exception>
    public T Value => IsOk ? _value! : throw new InvalidOperationException("Result is not ok.");
    private readonly T? _value = default;

    private ValueResult(IResult result, T? value)
    {
        _internalResult = result;
        _value = value;
    }

    /// <summary>
    /// Creates a successful result with the provided value.
    /// </summary>
    /// <param name="value">
    /// Value to store in the successful result.
    /// </param>
    /// <returns>
    /// Successful <see cref="ValueResult{T}"/> containing <paramref name="value"/>.
    /// </returns>
    public static ValueResult<T> Ok(T value) => new(SimpleResult.Ok(), value);

    /// <summary>
    /// Gets a cached failed result with no error message.
    /// </summary>
    /// <returns>
    /// Failed <see cref="ValueResult{T}"/> without error details.
    /// </returns>
    public static ValueResult<T> Error() => ErrorEmptyResult;

    /// <summary>
    /// Creates a failed value result from another failed result.
    /// </summary>
    /// <param name="result">
    /// Source failed result to copy errors from.
    /// </param>
    /// <returns>
    /// Failed <see cref="ValueResult{T}"/> with errors copied from <paramref name="result"/>.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="result"/> is successful.
    /// </exception>
    public static ValueResult<T> Error(IResult result) =>
        !result.IsOk
            ? new ValueResult<T>(result, default)
            : throw new InvalidOperationException("Cannot use successful results.");

    /// <summary>
    /// Creates a failed result with a single error message.
    /// </summary>
    /// <param name="errorMessage">
    /// Error message describing the failure.
    /// </param>
    /// <returns>
    /// Failed <see cref="ValueResult{T}"/> containing the provided error.
    /// </returns>
    public static ValueResult<T> Error(string errorMessage) =>
        new(SimpleResult.Error(errorMessage), default);
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

    /// <summary>
    /// Creates a successful result with no errors.
    /// </summary>
    /// <returns>
    /// Successful <see cref="Result"/> instance.
    /// </returns>
    public static Result Ok() => new(null, null);

    /// <summary>
    /// Creates a failed result with a single error message.
    /// </summary>
    /// <param name="errorMessage">
    /// Error message describing the failure.
    /// </param>
    /// <returns>
    /// Failed <see cref="Result"/> containing the provided error.
    /// </returns>
    public static Result Error(string errorMessage) => new(errorMessage, null);

    /// <summary>
    /// Creates a failed result with multiple error messages.
    /// </summary>
    /// <param name="errors">
    /// Collection of error messages describing the failure.
    /// </param>
    /// <returns>
    /// Failed <see cref="Result"/> containing all provided errors.
    /// </returns>
    public static Result Error(IEnumerable<string> errors) => new(null, errors.ToList());

    /// <summary>
    /// Creates a failed result by copying errors from another result.
    /// </summary>
    /// <param name="result">
    /// Source result whose errors should be copied.
    /// </param>
    /// <returns>
    /// Failed <see cref="Result"/> containing errors from <paramref name="result"/>.
    /// </returns>
    public static Result Error(IResult result) => Error(result.Errors);

    /// <summary>
    /// Appends an error message to this result.
    /// </summary>
    /// <param name="errorMessage">
    /// Error message to add.
    /// </param>
    public void AddError(string errorMessage)
    {
        _errors ??= new List<string>();
        _errors.Add(errorMessage);
    }

    /// <summary>
    /// Appends all errors from another result to this result.
    /// </summary>
    /// <param name="result">
    /// Result whose errors should be merged into this instance.
    /// </param>
    /// <returns>
    /// This <see cref="Result"/> instance after merging.
    /// </returns>
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
