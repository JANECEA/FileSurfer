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
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    public Task<IResult> InvokeAsync(ProgressReporter reporter, CancellationToken ct);
}

public abstract class FileOperation : IFileOperation
{
    private const int WaitBetweenMs = 5;

    protected IFileIoHandler FileIoHandler { get; }
    protected IFileSystemEntry[] Entries { get; }

    protected abstract string InvokeVerb { get; }

    protected FileOperation(IFileIoHandler fileIoHandler, IFileSystemEntry[] entries)
    {
        FileIoHandler = fileIoHandler;
        Entries = entries;
    }

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
    /// <param name="entry"><see cref="IFileSystemEntry"/> for the invoke-action.</param>
    /// <param name="index">Entry's index in <see cref="Entries"/>, in case it is useful.</param>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    protected abstract IResult InvokeAction(IFileSystemEntry entry, int index);
}
