using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace FileSurfer.Linux.Views;

public partial class PropertiesWindow : Window
{
    public PropertiesWindow() => InitializeComponent();

    private void CloseWindow(object sender, RoutedEventArgs args) => Close();

    private void KeyPressed(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Escape)
            Close();
    }
}
