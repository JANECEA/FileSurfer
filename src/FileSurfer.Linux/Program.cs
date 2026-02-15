using System;
using System.IO;
using Avalonia;
using Avalonia.Input.Platform;
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
using ReactiveUI.Avalonia;

namespace FileSurfer.Linux;

internal static class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        App.Bootstrap = new LinuxPlatformBootstrap();
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

public class LinuxPlatformBootstrap : IPlatformBootstrap
{
    private static readonly LinuxShellHandler LinuxShellHandler = new();

    public MainWindowViewModel GetViewModel(
        string initialDir,
        MainWindow mainWindow,
        Action<bool> setDarkMode
    )
    {
        LinuxFileIoHandler fileIoHandler = new();
        LinuxFileInfoProvider fileInfoProvider = new(LinuxShellHandler);
        IClipboard clipboard = mainWindow.Clipboard ?? throw new InvalidDataException();
        LocalClipboardManager clipboardManager = new(
            clipboard,
            mainWindow.StorageProvider,
            fileIoHandler,
            fileInfoProvider
        );

        AvaloniaDialogService dialogService = new(mainWindow);

        LocalFileSystem localFileSystem = new()
        {
            LocalFileInfoProvider = fileInfoProvider,
            IconProvider = new LinuxIconProvider(LinuxShellHandler),
            LocalClipboardManager = clipboardManager,
            ArchiveManager = new LocalArchiveManager(fileInfoProvider),
            FileIoHandler = new LinuxFileIoHandler(),
            BinInteraction = new LinuxBinInteraction(LinuxShellHandler),
            FileProperties = new LinuxFileProperties(new PropertiesVmFactory(mainWindow)),
            LocalShellHandler = LinuxShellHandler,
            GitIntegration = new LocalGitIntegration(LinuxShellHandler),
        };
        return new MainWindowViewModel(initialDir, localFileSystem, dialogService, setDarkMode);
    }

    public IDefaultSettingsProvider GetDefaultSettingsProvider() =>
        new LinuxDefaultSettingsProvider(LinuxShellHandler);
}
