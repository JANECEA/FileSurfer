using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace FileSurfer.Core.Views.Dialogs;

public partial class InputDialogWindow : Window
{
    private const char HideChar = '*';
    private const char NoHideChar = '\0';

    public string Context { get; init; } = string.Empty;

    public InputDialogWindow() => InitializeComponent();

    protected override void OnOpened(EventArgs e)
    {
        ContextBlock.Text = Context;
        base.OnOpened(e);
    }

    public void HideInput(bool hide) => InputBox.PasswordChar = hide ? HideChar : NoHideChar;

    private void CancelClicked(object? sender, RoutedEventArgs e) => Close(null);

    private void ContinueClicked(object? sender, RoutedEventArgs e) => Close(InputBox.Text);
}
