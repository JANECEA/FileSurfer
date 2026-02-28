using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace FileSurfer.Core.Views.Dialogs;

public partial class SuggestInputDialogWindow : Window
{
    public string Context { get; init; } = string.Empty;
    public string SuggestionLabel { get; init; } = string.Empty;
    public IEnumerable<string> Options { get; init; } = [];

    public SuggestInputDialogWindow() => InitializeComponent();

    protected override void OnOpened(EventArgs e)
    {
        ContextBlock.Text = Context;
        Suggestions.ItemsSource = Options;
        SuggestionLabelTextBlock.Text = SuggestionLabel;
        base.OnOpened(e);
    }

    private void CancelClicked(object? sender, RoutedEventArgs e) => Close(null);

    private void ContinueClicked(object? sender, RoutedEventArgs e) => Close(InputBox.Text);

    private void SuggestionsTapped(object? sender, TappedEventArgs e)
    {
        if (sender is ListBox { SelectedItem: string suggestion } listBox)
        {
            InputBox.Text = suggestion;
            listBox.SelectedItems?.Clear();
        }
    }
}
