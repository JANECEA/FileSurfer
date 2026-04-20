using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace FileSurfer.Core.Views.Dialogs;

/// <summary>
/// Displays an input dialog with selectable suggestions and contextual prompt text.
/// </summary>
public partial class SuggestInputDialogWindow : Window
{
    /// <summary>
    /// Gets the contextual text shown above the input field.
    /// </summary>
    public string Context { get; init; } = string.Empty;

    /// <summary>
    /// Gets the label displayed next to the suggestions list.
    /// </summary>
    public string SuggestionLabel { get; init; } = string.Empty;

    /// <summary>
    /// Gets the available suggestion options shown in the dialog.
    /// </summary>
    public IEnumerable<string> Options { get; init; } = [];

    /// <summary>
    /// Initializes the suggestion input dialog and loads its XAML components.
    /// </summary>
    public SuggestInputDialogWindow() => InitializeComponent();

    protected override void OnOpened(EventArgs e)
    {
        ContextBlock.Text = Context;
        Suggestions.ItemsSource = Options;
        SuggestionLabelTextBlock.Text = SuggestionLabel;
        base.OnOpened(e);
    }

    private void CancelClicked(object? sender, RoutedEventArgs? e) => Close(null);

    private void ContinueClicked(object? sender, RoutedEventArgs? e) => Close(InputBox.Text);

    private void SuggestionsTapped(object? sender, TappedEventArgs e)
    {
        if (sender is ListBox { SelectedItem: string suggestion } listBox)
        {
            InputBox.Text = suggestion;
            listBox.SelectedItems?.Clear();
        }
    }

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
