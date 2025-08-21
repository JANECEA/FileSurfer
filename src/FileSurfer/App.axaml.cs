using System;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Logging;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using FileSurfer.Models;
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
            SimpleResult result = ValidateArgs(desktop.Args, out string? initialDir);
            if (!result.IsOk)
            {
                desktop.Shutdown(1);
                return;
            }
            RequestedThemeVariant = FileSurferSettings.UseDarkMode
                ? ThemeVariant.Dark
                : ThemeVariant.Light;

            desktop.MainWindow = new MainWindow
            {
                DataContext = GetViewModel(initialDir ?? FileSurferSettings.OpenIn),
            };
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
            if (Directory.Exists(args[0]))
                directory = args[0];
            else
                return SimpleResult.Error($"Directory: \"{args[0]}\" does not exist.");

        return SimpleResult.Ok();
    }

    private static MainWindowViewModel GetViewModel(string initialDir)
    {
        WindowsFileInfoProvider fileInfoProvider = new();
        WindowsFileIOHandler fileIOHandler =
            new(fileInfoProvider, new WindowsFileRestorer(), FileSurferSettings.ShowDialogLimitB);
        WindowsShellHandler shellHandler = new();

        return new MainWindowViewModel(
            initialDir,
            fileIOHandler,
            fileInfoProvider,
            shellHandler,
            new GitVersionControl(shellHandler),
            new ClipboardManager(fileIOHandler, FileSurferSettings.NewImageName)
        );
    }
}
