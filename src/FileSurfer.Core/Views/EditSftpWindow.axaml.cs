using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using FileSurfer.Core.ViewModels;

namespace FileSurfer.Core.Views;

/// <summary>
/// TODO
/// </summary>
public partial class EditSftpWindow : Window
{
    /// <summary>
    /// TODO
    /// </summary>
    public EditSftpWindow(SftpConnectionViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    public EditSftpWindow()
    {
        InitializeComponent();
        DataContext = new SftpConnectionViewModel();
    }

    private void CloseWindow(object? sender = null, RoutedEventArgs? args = null) => Close();

    private void SaveAndClose(object? sender = null, RoutedEventArgs? args = null)
    {
        if (DataContext is SftpConnectionViewModel viewModel)
            viewModel.Save();

        CloseWindow();
    }

    private void KeyPressed(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            CloseWindow();

        if (e.Key == Key.Enter)
            SaveAndClose();
    }
}
