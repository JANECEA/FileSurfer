using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace FileSurfer.Core.Views;

public partial class ConfirmationDialogWindow : Window
{
    public string DialogTitle { get; init; } = string.Empty;
    public string Context { get; init; } = string.Empty;

    public ConfirmationDialogWindow() => InitializeComponent();

    protected override void OnOpened(EventArgs e)
    {
        ContextBlock.Text = Context;
        Title = DialogTitle;
        base.OnOpened(e);
    }

    private void OnYesClicked(object? sender = null, RoutedEventArgs? e = null) => Close(true);

    private void OnNoClicked(object? sender = null, RoutedEventArgs? e = null) => Close(false);

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        Close(false);
        base.OnClosing(e);
    }
}
