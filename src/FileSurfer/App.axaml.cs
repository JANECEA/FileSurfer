using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using FileSurfer.ViewModels;
using FileSurfer.Views;

namespace FileSurfer;

/// <summary>
/// The App class serves as the entry point for the FileSurfer application, handling application-wide initialization and setup.
/// <para>
/// It configures global resources, themes, and the main window, ensuring that the application's settings and appearance are properly applied.
/// </para>
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Initializes the application by loading XAML resources.
    /// </summary>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// Invokes <see cref="FileSurferSettings.LoadSettings"/> and sets <see cref="Application.RequestedThemeVariant"/> theme.
    /// <para>
    /// Configures the main window with the appropriate DataContext.
    /// </para>
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        FileSurferSettings.LoadSettings();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (FileSurferSettings.UseDarkMode)
                RequestedThemeVariant = ThemeVariant.Dark;
            else
                RequestedThemeVariant = ThemeVariant.Light;

            desktop.MainWindow = new MainWindow { DataContext = new MainWindowViewModel() };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
