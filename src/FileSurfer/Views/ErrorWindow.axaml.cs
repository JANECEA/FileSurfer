using Avalonia.Controls;
using Avalonia.Input;
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
