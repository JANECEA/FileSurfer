using System;
using System.Threading;
using System.Threading.Tasks;
using FileSurfer.Core.Models;
using FileSurfer.Core.Services.Dialogs;

namespace FileSurfer.Core.Services.FileOperations;

/// <summary>
/// Represents a file operation in the context of the <see cref="FileSurfer"/> app.
/// </summary>
public interface IFileOperation
{
    /// <summary>
    /// Invokes the file operation.
    /// Implementations of this method should apply the effects of the operation.
    /// </summary>
    /// <param name="reporter">
    /// Progress reporter used to publish operation progress updates.
    /// </param>
    /// <param name="ct">
    /// Cancellation token used to stop the operation.
    /// </param>
    /// <returns>
    /// A task that returns the operation result, including any error details if execution fails.
    /// </returns>
    public Task<IResult> InvokeAsync(ProgressReporter reporter, CancellationToken ct);
}

/// <summary>
/// Provides a shared execution pipeline for batched file operations with progress reporting and
/// cancellation handling.
/// </summary>
public abstract class FileOperation : IFileOperation
{
    private const int WaitBetweenMs = 5;

    /// <summary>
    /// Gets the file I/O handler used by derived operations to perform entry-level actions.
    /// </summary>
    protected IFileIoHandler FileIoHandler { get; }

    /// <summary>
    /// Gets the entries targeted by this operation, in the order they are processed.
    /// </summary>
    protected IFileSystemEntry[] Entries { get; }

    /// <summary>
    /// Gets the operation verb used in progress messages (for example, "Deleting").
    /// </summary>
    protected abstract string InvokeVerb { get; }

    /// <summary>
    /// Initializes a file operation with the I/O handler and target entries.
    /// </summary>
    /// <param name="fileIoHandler">
    /// File I/O handler used to perform concrete entry operations.
    /// </param>
    /// <param name="entries">
    /// Entries the operation should process.
    /// </param>
    protected FileOperation(IFileIoHandler fileIoHandler, IFileSystemEntry[] entries)
    {
        FileIoHandler = fileIoHandler;
        Entries = entries;
    }

    /// <summary>
    /// Runs the operation for all entries using the configured action implementation.
    /// </summary>
    /// <param name="reporter">
    /// Progress reporter used to publish operation progress updates.
    /// </param>
    /// <param name="ct">
    /// Cancellation token used to stop execution.
    /// </param>
    /// <returns>
    /// A task that returns the aggregated operation result for all processed entries.
    /// </returns>
    public Task<IResult> InvokeAsync(ProgressReporter reporter, CancellationToken ct) =>
        Task.Run(() => InvokeInternal(InvokeAction, InvokeVerb, reporter, ct), ct);

    private protected async Task<IResult> InvokeInternal(
        Func<IFileSystemEntry, int, IResult> action,
        string opVerb,
        ProgressReporter reporter,
        CancellationToken ct
    )
    {
        CountingReporter rep = new(reporter, Entries.Length);

        Result result = Result.Ok();
        for (int i = 0; i < Entries.Length; i++)
        {
            rep.ReportItem($"{opVerb}: \"{Entries[i].Name}\"");
            result.MergeResult(action(Entries[i], i));
            if (i == Entries.Length - 1)
                return result;

            try
            {
                await Task.Delay(WaitBetweenMs, ct).ConfigureAwait(false);
                ct.ThrowIfCancellationRequested();
            }
            catch
            {
                return SimpleResult.Error("Operation was cancelled.");
            }
        }
        return result;
    }

    /// <summary>
    /// Represents an invoke-action invoked on <see cref="IFileSystemEntry"/> from <see cref="Entries"/>.
    /// </summary>
    /// <param name="entry">
    /// The entry currently being processed.
    /// </param>
    /// <param name="index">
    /// Zero-based index of <paramref name="entry"/> in <see cref="Entries"/>.
    /// </param>
    /// <returns>
    /// The operation result for the single processed entry.
    /// </returns>
    protected abstract IResult InvokeAction(IFileSystemEntry entry, int index);
}
