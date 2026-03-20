using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace FileSurfer.Core.Views.Dialogs;

public partial class ConfirmationDialogWindow : Window
{
    public string Question { get; init; } = string.Empty;

    public ConfirmationDialogWindow() => InitializeComponent();

    protected override void OnOpened(EventArgs e)
    {
        QuestionBlock.Text = Question;
        base.OnOpened(e);
    }

    private void OnYesClicked(object? sender, RoutedEventArgs e) => Close(true);

    private void OnNoClicked(object? sender, RoutedEventArgs e) => Close(false);

    private void KeyPressed(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter)
        {
            e.Handled = true;
            Close(false);
        }
        if (e.Key is Key.Escape)
        {
            e.Handled = true;
            Close(true);
        }
    }
}
