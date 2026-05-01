using System;
using System.Threading.Tasks;
using Avalonia.Input.Platform;
using FileSurfer.Core.Models;

namespace FileSurfer.Core.Services.FileOperations;

/// <summary>
/// Represents an abstraction for the operating system clipboard.
/// </summary>
public interface IOsClipboardProxy
{
    /// <summary>
    /// Executes an asynchronous clipboard operation on the UI thread and returns its result.
    /// </summary>
    /// <typeparam name="T">
    /// The result type produced by the provided operation.
    /// </typeparam>
    /// <param name="operation">
    /// The asynchronous clipboard operation to run against the current clipboard instance.
    /// </param>
    /// <returns>
    /// A task that resolves to the value returned by <paramref name="operation"/>.
    /// </returns>
    Task<T> ExecuteAsync<T>(Func<IClipboard, Task<T>> operation);

    /// <summary>
    /// Executes an asynchronous clipboard operation on the UI thread.
    /// </summary>
    /// <param name="operation">
    /// The asynchronous clipboard operation to run against the current clipboard instance.
    /// </param>
    /// <returns>
    /// A task that completes when the provided operation has finished.
    /// </returns>
    Task ExecuteAsync(Func<IClipboard, Task> operation);

    /// <summary>
    /// Asynchronously clears the OS clipboard on the UI thread by setting a marker object.
    /// </summary>
    /// <returns>
    /// A task that completes when the clearing operation has finished.
    /// </returns>
    Task<IResult> ClearAsync();

    /// <summary>
    /// Copies FileSurfer entries to the OS clipboard as storage items.
    /// </summary>
    /// <param name="entries">
    /// The file-system entries to place on the OS clipboard.
    /// </param>
    /// <returns>
    /// A task that returns the operation result, including error details when clipboard population fails.
    /// </returns>
    Task<IResult> CopyToOsClipboardAsync(IFileSystemEntry[] entries);
}