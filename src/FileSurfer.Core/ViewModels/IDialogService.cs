using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using FileSurfer.Core.Views.Dialogs;

namespace FileSurfer.Core.ViewModels;

public interface IDialogService
{
    public void InfoDialog(string title, string info);

    public Task<bool> ConfirmationDialog(string title, string question);

    public Task<string?> InputDialog(string title, string context, bool secret);

    public Task<string?> SuggestInputDialog(
        string title,
        string context,
        IReadOnlyList<string> options
    );
}

public sealed class AvaloniaDialogService : IDialogService
{
    private readonly Window _parentWindow;

    public AvaloniaDialogService(Window parentWindow) => _parentWindow = parentWindow;

    public void InfoDialog(string title, string info) =>
        Dispatcher.UIThread.Post(() =>
        {
            new InfoDialogWindow { Title = title, Message = info }.ShowDialog(_parentWindow);
        });

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
        IReadOnlyList<string> options
    ) =>
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            SuggestInputDialogWindow dialog = new()
            {
                Title = title,
                Context = context,
                Options = options,
            };
            string? result = await dialog.ShowDialog<string?>(_parentWindow);
            return string.IsNullOrEmpty(result) ? null : result;
        });
}
