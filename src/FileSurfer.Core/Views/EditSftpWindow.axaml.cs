using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using FileSurfer.Core.ViewModels;

namespace FileSurfer.Core.Views;

/// <summary>
/// Represents the window to edit or create new <see cref="SftpConnectionViewModel"/>
/// </summary>
public partial class EditSftpWindow : Window
{
    private readonly SftpConnectionViewModel _vmCopy;
    private readonly SftpConnectionViewModel _vmOriginal;

    public EditSftpWindow(SftpConnectionViewModel viewModel)
    {
        InitializeComponent();

        _vmOriginal = viewModel;
        _vmCopy = _vmOriginal.Copy();
        DataContext = _vmCopy;
    }

    private void CloseWindow(object? sender = null, RoutedEventArgs? args = null) => Close();

    private void SaveAndClose(object? sender = null, RoutedEventArgs? args = null)
    {
        _vmOriginal.Save(_vmCopy);
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
