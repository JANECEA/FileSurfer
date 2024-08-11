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
        if (sender is ListBox listBox && DataContext is MainWindowViewModel viewModel)
        {
            if (listBox.SelectedItem is FileSystemEntry entry)
                viewModel.OpenEntry(entry);
            else
                viewModel.GoUp();
        }
    }

    private void MouseButtonPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;
        PointerPointProperties properties = e.GetCurrentPoint(this).Properties;

        if (properties.IsXButton1Pressed)
            viewModel.GoBack();

        else if (properties.IsXButton2Pressed)
            viewModel.GoForward();
    }
}

