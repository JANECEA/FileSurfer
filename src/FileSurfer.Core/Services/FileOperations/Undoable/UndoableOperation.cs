using System;
using System.Threading;
using System.Threading.Tasks;
using FileSurfer.Core.Models;
using FileSurfer.Core.Services.Dialogs;

namespace FileSurfer.Core.Services.FileOperations.Undoable;

public abstract class UndoableOperation : IUndoableFileOperation
{
    protected IFileIoHandler FileIoHandler { get; }
    protected IFileSystemEntry[] Entries { get; }

    protected abstract string InvokeOpName { get; }
    protected abstract string UndoOpName { get; }

    protected UndoableOperation(IFileIoHandler fileIoHandler, IFileSystemEntry[] entries)
    {
        FileIoHandler = fileIoHandler;
        Entries = entries;
    }

    public async Task<IResult> Invoke(ProgressReporter reporter, CancellationToken ct) =>
        await InvokeInternal(InvokeAction, InvokeOpName, reporter, ct);

    public async Task<IResult> Undo(ProgressReporter reporter, CancellationToken ct) =>
        await InvokeInternal(UndoAction, UndoOpName, reporter, ct);

    private async Task<IResult> InvokeInternal(
        Func<IFileSystemEntry, int, IResult> action,
        string opName,
        ProgressReporter reporter,
        CancellationToken ct
    )
    {
        CountingReporter rep = new(reporter, Entries.Length);

        Result result = Result.Ok();
        for (int i = 0; i < Entries.Length; i++)
        {
            if (ct.IsCancellationRequested)
                return SimpleResult.Error("Operation was cancelled.");

            rep.ReportItem($"{opName}: \"{Entries[i].Name}\"");
            result.MergeResult(await Task.Run(() => action(Entries[i], i), ct));
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

    /// <summary>
    /// Represents a undo-action invoked on <see cref="IFileSystemEntry"/> from <see cref="Entries"/>.
    /// </summary>
    /// <param name="entry"><see cref="IFileSystemEntry"/> for the undo-action.</param>
    /// <param name="index">Entry's index in <see cref="Entries"/>, in case it is useful.</param>
    /// <returns>A <see cref="IResult"/> representing the result of the operation and potential errors.</returns>
    protected abstract IResult UndoAction(IFileSystemEntry entry, int index);
}
