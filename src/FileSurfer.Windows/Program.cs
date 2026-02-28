using System;
using System.IO;
using Avalonia;
using Avalonia.Input.Platform;
using FileSurfer.Core;
using FileSurfer.Core.Models;
using FileSurfer.Core.Services.Dialogs;
using FileSurfer.Core.Services.FileOperations;
using FileSurfer.Core.Services.VersionControl;
using FileSurfer.Core.ViewModels;
using FileSurfer.Core.Views;
using FileSurfer.Windows.Models.FileInformation;
using FileSurfer.Windows.Services.FileOperations;
using FileSurfer.Windows.Services.Shell;
using ReactiveUI.Avalonia;

namespace FileSurfer.Windows;

internal static class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        App.Bootstrap = new WindowsPlatformBootstrap();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    private static AppBuilder BuildAvaloniaApp() =>
        AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
}

public class WindowsPlatformBootstrap : IPlatformBootstrap
{
    public MainWindowViewModel GetViewModel(
        string initialDir,
        MainWindow mainWindow,
        Action<bool> setDarkMode
    )
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
        return new MainWindowViewModel(initialDir, localFileSystem, dialogService, setDarkMode);
    }

    public IDefaultSettingsProvider GetDefaultSettingsProvider() =>
        new WindowsDefaultSettingsProvider();
}
