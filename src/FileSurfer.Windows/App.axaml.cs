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
using FileSurfer.Windows.Models.FileInformation;
using FileSurfer.Windows.Models.FileOperations;
using FileSurfer.Windows.Models.Shell;

namespace FileSurfer.Windows;

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
        FileSurferSettings.Initialize(new WindowsDefaultSettingsProvider());
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            SimpleResult result = ValidateArgs(desktop.Args, out string? initialDir);
            if (!result.IsOk)
            {
                desktop.Shutdown(1);
                return;
            }

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

    private MainWindowViewModel GetViewModel(string initialDir, MainWindow mainWindow)
    {
        WindowsFileInfoProvider fileInfoProvider = new();
        WindowsFileIoHandler fileIoHandler = new(
            fileInfoProvider,
            FileSurferSettings.ShowDialogLimitB
        );
        WindowsShellHandler shellHandler = new();
        IClipboard clipboard = mainWindow.Clipboard ?? throw new InvalidDataException();
        LocalClipboardManager clipboardManager = new(
            clipboard,
            mainWindow.StorageProvider,
            fileIoHandler,
            fileInfoProvider
        );
        WindowsBinInteraction binInteraction = new(
            FileSurferSettings.ShowDialogLimitB,
            fileInfoProvider
        );

        AvaloniaDialogService dialogService = new(mainWindow);

        LocalFileSystem localFileSystem = new()
        {
            LocalFileInfoProvider = fileInfoProvider,
            IconProvider = new WindowsIconProvider(),
            LocalClipboardManager = clipboardManager,
            ArchiveManager = new LocalArchiveManager(fileInfoProvider),
            FileIoHandler = fileIoHandler,
            BinInteraction = binInteraction,
            FileProperties = new WindowsFileProperties(),
            LocalShellHandler = shellHandler,
            GitIntegration = new LocalGitIntegration(shellHandler),
        };
        return new MainWindowViewModel(initialDir, localFileSystem, dialogService, SetDarkMode);
    }

    private void SetDarkMode(bool darkMode) =>
        RequestedThemeVariant = darkMode ? ThemeVariant.Dark : ThemeVariant.Light;
}
