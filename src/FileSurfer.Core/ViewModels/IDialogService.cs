using Avalonia.Controls;
using Avalonia.Threading;
using FileSurfer.Core.Views;

namespace FileSurfer.Core.ViewModels;

public interface IDialogService
{
    public void InfoDialog(string title, string info);

    public bool ConfirmationDialog(string title, string question);

    public string? InputDialog(string title, string context, bool secret);
}

public sealed class AvaloniaDialogService : IDialogService
{
    private readonly Window _parentWindow;

    public AvaloniaDialogService(Window parentWindow) => _parentWindow = parentWindow;

    public void InfoDialog(string title, string info) =>
        Dispatcher.UIThread.Post(() =>
        {
            new InfoDialogWindow { DialogTitle = title, Message = info }.ShowDialog(_parentWindow);
        });

    public bool ConfirmationDialog(string title, string question) =>
        Dispatcher
            .UIThread.InvokeAsync(async () =>
                await new ConfirmationDialogWindow
                {
                    DialogTitle = title,
                    Context = question,
                }.ShowDialog<bool>(_parentWindow)
            )
            .GetAwaiter()
            .GetResult();

    public string? InputDialog(string title, string context, bool secret) =>
        throw new System.NotImplementedException();
}
