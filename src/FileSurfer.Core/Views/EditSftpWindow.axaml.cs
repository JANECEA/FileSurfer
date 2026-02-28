using System;
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
    private const string EditingWindowTitle = "Edit SFTP Connection";
    private const string AddingWindowTitle = "Add new SFTP Connection";

    private readonly SftpConnectionViewModel? _vmCopy;
    private readonly SftpConnectionViewModel? _vmOriginal;

    public SftpConnectionViewModel ViewModel
    {
        init
        {
            _vmOriginal = value;
            _vmCopy = _vmOriginal.Copy();
            DataContext = _vmCopy;
            Title = _vmOriginal.CreateOnSave ? AddingWindowTitle : EditingWindowTitle;
        }
    }

    public EditSftpWindow() => InitializeComponent();

    private void CloseWindow(object? sender = null, RoutedEventArgs? args = null) => Close();

    private void SaveAndClose(object? sender = null, RoutedEventArgs? args = null)
    {
        if (_vmOriginal is null || _vmCopy is null)
            throw new InvalidOperationException("The Viewmodel field was not set during creation");

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
