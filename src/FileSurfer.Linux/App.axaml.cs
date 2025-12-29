using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using FileSurfer.Core;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileOperations;
using FileSurfer.Core.Models.VersionControl;
using FileSurfer.Core.ViewModels;
using FileSurfer.Core.Views;
using FileSurfer.Linux.Models.FileInformation;
using FileSurfer.Linux.Models.FileOperations;
using FileSurfer.Linux.Models.Shell;
using FileSurfer.Linux.ViewModels;

namespace FileSurfer.Linux;

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
    /// Invokes <see cref="FileSurferSettings.Initialize"/> and sets <see cref="Application.RequestedThemeVariant"/> theme.
    /// <para>
    /// Configures the main window with the appropriate DataContext.
    /// </para>
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        FileSurferSettings.Initialize(new LinuxDefaultSettingsProvider(new LinuxShellHandler()));
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            SimpleResult result = ValidateArgs(desktop.Args, out string? initialDir);
            if (!result.IsOk)
            {
                desktop.Shutdown(1);
                return;
            }
            RequestedThemeVariant = FileSurferSettings.UseDarkMode
                ? ThemeVariant.Dark
                : ThemeVariant.Light;

            MainWindow mainWindow = new();
            mainWindow.DataContext = GetViewModel(
                initialDir ?? FileSurferSettings.OpenIn,
                mainWindow
            );
            desktop.MainWindow = mainWindow;
            desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnMainWindowClose;
        }
        base.OnFrameworkInitializationCompleted();
    }

    private static SimpleResult ValidateArgs(string[]? args, out string? directory)
    {
        directory = null;

        if (args?.Length > 1)
            return SimpleResult.Error("Incorrect number of arguments.");

        if (args?.Length == 1)
            directory = args[0];

        return SimpleResult.Ok();
    }

    private static MainWindowViewModel GetViewModel(string initialDir, MainWindow mainWindow)
    {
        LinuxFileIOHandler fileIOHandler = new();
        LinuxShellHandler shellHandler = new();
        IClipboard clipboard = mainWindow.Clipboard ?? throw new InvalidDataException();
        ClipboardManager clipboardManager = new(
            clipboard,
            mainWindow.StorageProvider,
            fileIOHandler,
            FileSurferSettings.NewImageName
        );

        return new MainWindowViewModel(
            initialDir,
            fileIOHandler,
            new LinuxBinInteraction(shellHandler),
            new LinuxFileProperties(new PropertiesVmFactory(mainWindow)),
            new LinuxFileInfoProvider(shellHandler),
            new LinuxIconProvider(shellHandler),
            shellHandler,
            new GitVersionControl(shellHandler),
            clipboardManager
        );
    }
}
