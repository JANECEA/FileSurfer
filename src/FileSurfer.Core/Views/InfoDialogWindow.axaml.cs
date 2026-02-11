using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace FileSurfer.Core.Views;

/// <summary>
/// Represents an error dialog in the context of the <see cref="FileSurfer"/> app.
/// </summary>
public partial class InfoDialogWindow : Window
{
    public string DialogTitle { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;

    public InfoDialogWindow() => InitializeComponent();

    /// <summary>
    /// Assigns <see cref="Message"/> to <see cref="InfoDialogWindow.InfoBlock"/> before opening.
    /// </summary>
    protected override void OnOpened(EventArgs e)
    {
        Title = DialogTitle;
        InfoBlock.Text = Message;
        base.OnOpened(e);
    }

    private void KeyPressed(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }

    private void CloseWindow(object? sender, RoutedEventArgs e) => Close();
}
