using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using FileSurfer.Models.FileInformation;
using FileSurfer.Models.FileOperations;
using FileSurfer.Models.Shell;
using FileSurfer.Models.VersionControl;
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
            RequestedThemeVariant = FileSurferSettings.UseDarkMode
                ? ThemeVariant.Dark
                : ThemeVariant.Light;

            desktop.MainWindow = new MainWindow { DataContext = GetViewModel() };
            desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnMainWindowClose;
        }
        base.OnFrameworkInitializationCompleted();
    }

    private static MainWindowViewModel GetViewModel()
    {
        WindowsFileInfoProvider fileInfoProvider = new();
        WindowsFileIOHandler fileIOHandler =
            new(fileInfoProvider, new WindowsFileRestorer(), FileSurferSettings.ShowDialogLimitB);
        WindowsShellHandler shellHandler = new();

        return new MainWindowViewModel(
            fileIOHandler,
            fileInfoProvider,
            shellHandler,
            new GitVersionControl(shellHandler),
            new ClipboardManager(fileIOHandler, FileSurferSettings.NewImageName)
        );
    }
}
