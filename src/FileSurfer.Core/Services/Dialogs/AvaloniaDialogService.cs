using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using FileSurfer.Core.Views.Dialogs;

namespace FileSurfer.Core.Services.Dialogs;

[SuppressMessage("ReSharper", "MethodSupportsCancellation")]
public sealed class AvaloniaDialogService : IDialogService
{
    private const int ShowOpDialogDelayMs = 1000;
    private readonly Window _parentWindow;

    public AvaloniaDialogService(Window parentWindow) => _parentWindow = parentWindow;

    public void InfoDialog(string title, string info) =>
        Dispatcher.UIThread.Post(() =>
        {
            new InfoDialogWindow { Title = title, Message = info }.ShowDialog(_parentWindow);
        });

    private static async Task<T> ProgressDialogInternal<T>(
        string title,
        Task<T> opTask,
        ProgressReporter reporter,
        CancellationTokenSource cts
    )
    {
        Task waitTask = Task.Delay(ShowOpDialogDelayMs);
        await Task.WhenAny(opTask, waitTask);
        if (opTask.IsCompleted)
            return await opTask;

        bool dialogClosedByUser = false;
        ProgressDialogWindow dialog = new()
        {
            Title = title,
            Reporter = reporter,
            Cts = cts,
        };
        dialog.Closed += (_, _) => dialogClosedByUser = true;

        await Dispatcher.UIThread.InvokeAsync(dialog.Show);
        try
        {
            T result = await opTask;
            return result;
        }
        finally
        {
            cts.Dispose();
            if (!dialogClosedByUser)
                await Dispatcher.UIThread.InvokeAsync(dialog.Close);
        }
    }

    public async Task<T> ProgressDialog<T>(string title, ReportingOperation<T> operation)
    {
        ProgressReporter reporter = new();
        CancellationTokenSource cts = new();
        Task<T> opTask = Task.Run(() => operation(reporter, cts.Token));
        return await ProgressDialogInternal(title, opTask, reporter, cts);
    }

    public async Task<T> ProgressDialog<T>(string title, CancellableOperation<T> operation)
    {
        ProgressReporter reporter = new();
        _ = new IndeterminateReporter(reporter);
        CancellationTokenSource cts = new();
        Task<T> opTask = Task.Run(() => operation(cts.Token));
        return await ProgressDialogInternal(title, opTask, reporter, cts);
    }

    public async Task<bool> ConfirmationDialog(string title, string question)
    {
        bool? result = await Dispatcher.UIThread.InvokeAsync(async () =>
            await new ConfirmationDialogWindow
            {
                Title = title,
                Question = question,
            }.ShowDialog<bool?>(_parentWindow)
        );
        return result is true;
    }

    public async Task<string?> InputDialog(string title, string context, bool secret) =>
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            InputDialogWindow dialog = new() { Title = title, Context = context };
            dialog.HideInput(secret);

            string? result = await dialog.ShowDialog<string?>(_parentWindow);
            return string.IsNullOrEmpty(result) ? null : result;
        });

    public async Task<string?> SuggestInputDialog(
        string title,
        string context,
        string suggestionLabel,
        IEnumerable<string> options
    ) =>
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            SuggestInputDialogWindow dialog = new()
            {
                Title = title,
                Context = context,
                Options = options,
                SuggestionLabel = suggestionLabel,
            };
            string? result = await dialog.ShowDialog<string?>(_parentWindow);
            return string.IsNullOrEmpty(result) ? null : result;
        });
}
