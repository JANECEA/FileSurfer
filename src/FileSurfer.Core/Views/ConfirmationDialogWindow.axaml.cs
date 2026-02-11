using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace FileSurfer.Core.Views;

public partial class ConfirmationDialogWindow : Window
{
    public string DialogTitle { get; init; } = string.Empty;
    public string Question { get; init; } = string.Empty;

    public ConfirmationDialogWindow() => InitializeComponent();

    protected override void OnOpened(EventArgs e)
    {
        QuestionBlock.Text = Question;
        Title = DialogTitle;
        base.OnOpened(e);
    }

    private void OnYesClicked(object? sender, RoutedEventArgs e) => Close(true);

    private void OnNoClicked(object? sender, RoutedEventArgs e) => Close(false);
}
