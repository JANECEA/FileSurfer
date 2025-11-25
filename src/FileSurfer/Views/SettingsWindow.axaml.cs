using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using FileSurfer.ViewModels;

namespace FileSurfer.Views;

/// <summary>
/// Represents the settings window in the context of the <see cref="FileSurfer"/> app.
/// </summary>
public partial class SettingsWindow : Window
{
    /// <summary>
    /// Creates a new <see cref="SettingsWindow"/>.
    /// </summary>
    public SettingsWindow()
    {
        InitializeComponent();
        DataContext = new SettingsWindowViewModel();
    }

    private void ResetToDefaults(object sender, RoutedEventArgs args)
    {
        if (DataContext is SettingsWindowViewModel viewModel)
        {
            DataContext = null;
            viewModel.ResetToDefault();
            DataContext = viewModel;
        }
    }

    private void CloseWindow(object? sender = null, RoutedEventArgs? args = null) => Close();

    private void SaveAndClose(object sender, RoutedEventArgs args)
    {
        if (DataContext is SettingsWindowViewModel viewModel)
            viewModel.Save();

        CloseWindow();
    }

    private void KeyPressed(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            CloseWindow();
    }
}
