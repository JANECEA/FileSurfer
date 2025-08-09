using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;

namespace FileSurfer.Views;

/// <summary>
/// Represents the settings window in the context of the <see cref="FileSurfer"/> app.
/// </summary>
public partial class SettingsWindow : MainWindow
{
    /// <summary>
    /// Creates a new <see cref="SettingsWindow"/>.
    /// </summary>
    public SettingsWindow()
    {
        InitializeComponent();
        DataContext = new ViewModels.SettingsWindowViewModel();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
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
