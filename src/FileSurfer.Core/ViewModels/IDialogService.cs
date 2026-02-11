using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using FileSurfer.Core.Views;

namespace FileSurfer.Core.ViewModels;

public interface IDialogService
{
    public void InfoDialog(string title, string info);

    public Task<bool> ConfirmationDialog(string title, string question);

    public Task<string?> InputDialog(string title, string context, bool secret);
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

    public async Task<bool> ConfirmationDialog(string title, string question)
    {
        bool? result = await Dispatcher.UIThread.Invoke(async () =>
            await new ConfirmationDialogWindow
            {
                DialogTitle = title,
                Question = question,
            }.ShowDialog<bool?>(_parentWindow)
        );
        return result is true;
    }

    public Task<string?> InputDialog(string title, string context, bool secret) =>
        throw new System.NotImplementedException();
}
