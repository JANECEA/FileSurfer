using Avalonia.Controls;
using Avalonia.Interactivity;

namespace FileSurfer.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        new ErrorWindow("This is indeed a message").Show();
    }

    private void OpenSettingsWindow(object sender, RoutedEventArgs args) => 
        new SettingsWindow().Show();
}