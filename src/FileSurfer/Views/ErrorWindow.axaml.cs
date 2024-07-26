using Avalonia.Controls;
using Avalonia.Interactivity;

namespace FileSurfer.Views;

public partial class ErrorWindow : Window
{
    public ErrorWindow(string errorMessage)
    {
        InitializeComponent();
        ErrorBlock.Text = errorMessage;
    }

    private void CloseWindow(object sender, RoutedEventArgs args) => Close();
}