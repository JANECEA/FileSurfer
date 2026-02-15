using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using FileSurfer.Core.Models;
using FileSurfer.Core.ViewModels;
using FileSurfer.Core.Views;

namespace FileSurfer.Core;

/// <summary>
/// The App class serves as the entry point for the FileSurfer application, handling application-wide initialization and setup.
/// <para>
/// It configures global resources, themes, and the main window, ensuring that the application's settings and appearance are properly applied.
/// </para>
/// </summary>
public partial class App : Application
{
    public static IPlatformBootstrap? Bootstrap { get; set; }

    /// <summary>
    /// Initializes the application by loading XAML resources.
    /// </summary>
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <summary>
    /// Invokes <see cref="FileSurferSettings.Initialize"/> and sets <see cref="Application.RequestedThemeVariant"/> theme.
    /// <para>
    /// Configures the main window with the appropriate DataContext.
    /// </para>
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        if (Bootstrap is null)
            throw new InvalidOperationException("Platform bootstrap is not initialized.");

        FileSurferSettings.Initialize(Bootstrap.GetDefaultSettingsProvider());
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            SimpleResult result = ValidateArgs(desktop.Args, out string? initialDir);
            if (!result.IsOk)
            {
                desktop.Shutdown(1);
                return;
            }

            MainWindow mainWindow = new();
            mainWindow.DataContext = Bootstrap.GetViewModel(
                initialDir ?? FileSurferSettings.OpenIn,
                mainWindow,
                SetDarkMode
            );
            desktop.MainWindow = mainWindow;
            desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnMainWindowClose;
        }
        base.OnFrameworkInitializationCompleted();
    }

    private void SetDarkMode(bool darkMode) =>
        RequestedThemeVariant = darkMode ? ThemeVariant.Dark : ThemeVariant.Light;

    private static SimpleResult ValidateArgs(string[]? args, out string? directory)
    {
        directory = null;

        if (args?.Length > 1)
            return SimpleResult.Error("Incorrect number of arguments.");

        if (args?.Length == 1)
            directory = args[0];

        return SimpleResult.Ok();
    }
}

/// <summary>
/// Used to initialize the app with platform specific settings
/// </summary>
public interface IPlatformBootstrap
{
    public MainWindowViewModel GetViewModel(
        string initialDir,
        MainWindow mainWindow,
        Action<bool> setDarkMode
    );

    public IDefaultSettingsProvider GetDefaultSettingsProvider();
}
