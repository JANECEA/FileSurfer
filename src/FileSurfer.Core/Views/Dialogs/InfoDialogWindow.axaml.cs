using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace FileSurfer.Core.Views.Dialogs;

/// <summary>
/// Represents an information dialog in the <see cref="FileSurfer"/> app.
/// </summary>
public partial class InfoDialogWindow : Window
{
    /// <summary>
    /// Gets the message text shown in the information dialog.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Initializes the information dialog window and loads its XAML components.
    /// </summary>
    public InfoDialogWindow() => InitializeComponent();

    protected override void OnOpened(EventArgs e)
    {
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
