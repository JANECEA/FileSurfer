using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace FileSurfer.Core.Views;

/// <summary>
/// Displays file or directory properties in a dedicated dialog window.
/// </summary>
public partial class PropertiesWindow : Window
{
    /// <summary>
    /// Initializes the properties window and loads its XAML components.
    /// </summary>
    public PropertiesWindow() => InitializeComponent();

    private void CloseWindow(object sender, RoutedEventArgs args) => Close();

    private void KeyPressed(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Escape)
            Close();
    }
}
