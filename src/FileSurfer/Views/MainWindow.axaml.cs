using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using FileSurfer.ViewModels;

namespace FileSurfer.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OpenSettingsWindow(object sender, RoutedEventArgs args) =>
        new SettingsWindow().Show();

    private void EntryDoubleTapped(object sender, TappedEventArgs e)
    {
        if (
            sender is ListBox listBox
            && listBox.SelectedItem is FileSystemEntry entry
            && DataContext is MainWindowViewModel viewModel
        )
            viewModel.OpenEntry(entry);
    }
}

