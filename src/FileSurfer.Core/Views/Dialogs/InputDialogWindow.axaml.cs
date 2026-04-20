using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace FileSurfer.Core.Views.Dialogs;

/// <summary>
/// Displays a text input dialog with optional input masking.
/// </summary>
public partial class InputDialogWindow : Window
{
    private const char HideChar = '*';
    private const char NoHideChar = '\0';

    /// <summary>
    /// Gets the contextual message displayed above the input field.
    /// </summary>
    public string Context { get; init; } = string.Empty;

    /// <summary>
    /// Initializes the input dialog window and loads its XAML components.
    /// </summary>
    public InputDialogWindow() => InitializeComponent();

    protected override void OnOpened(EventArgs e)
    {
        ContextBlock.Text = Context;
        base.OnOpened(e);
    }

    /// <summary>
    /// Enables or disables masking of typed input.
    /// </summary>
    public void HideInput(bool hide) => InputBox.PasswordChar = hide ? HideChar : NoHideChar;

    private void CancelClicked(object? sender, RoutedEventArgs? e) => Close(null);

    private void ContinueClicked(object? sender, RoutedEventArgs? e) => Close(InputBox.Text);

    private void KeyPressed(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter && ContinueButton.IsEnabled)
        {
            e.Handled = true;
            ContinueClicked(null, null);
        }
        if (e.Key is Key.Escape)
        {
            e.Handled = true;
            CancelClicked(null, null);
        }
    }
}
