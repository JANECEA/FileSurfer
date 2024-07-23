using Avalonia.Controls;
using Avalonia.Interactivity;

namespace FileSurfer.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OpenSettingsWindow(object sender, RoutedEventArgs args) => 
        new SettingsWindow().Show();
}