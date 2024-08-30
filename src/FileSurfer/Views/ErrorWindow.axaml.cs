using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;

namespace FileSurfer.Views;

/// <summary>
/// Represents an error dialog in the context of <see cref="FileSurfer"/>
/// </summary>
public partial class ErrorWindow : Window
{
    /// <summary>
    /// Holds text shown in <see cref="ErrorBlock"/>.
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// Creates a new <see cref="ErrorWindow"/> error dialog.
    /// </summary>
    public ErrorWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Assigns <see cref="ErrorMessage"/> to <see cref="ErrorBlock"/> before opening.
    /// </summary>
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        ErrorBlock.Text = ErrorMessage;
    }

    private void CloseWindow(object sender, RoutedEventArgs args) => Close();

    private void KeyPressed(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            OnEnterPressed(e);
    }

    private void OnEnterPressed(KeyEventArgs e)
    {
        e.Handled = true;
        Close();
    }
}
