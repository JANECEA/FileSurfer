using System;
using System.IO;
using Avalonia;
using Avalonia.Input.Platform;
using FileSurfer.Core;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.Sftp;
using FileSurfer.Core.Services.Dialogs;
using FileSurfer.Core.Services.FileOperations;
using FileSurfer.Core.Services.VersionControl;
using FileSurfer.Core.ViewModels;
using FileSurfer.Core.Views;
using FileSurfer.Linux.Models.FileInformation;
using FileSurfer.Linux.Services.FileOperations;
using FileSurfer.Linux.Services.Shell;
using ReactiveUI.Avalonia;

namespace FileSurfer.Linux;

/// <summary>
/// Starts the Linux Avalonia application and configures the app builder.
/// </summary>
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

/// <summary>
/// Wires Linux-specific services and dependencies into the main application view model.
/// </summary>
public class LinuxPlatformBootstrap : IPlatformBootstrap
{
    private static readonly LinuxShellHandler LinuxShellHandler = new();

    public MainWindowViewModel GetViewModel(
        string initialDir,
        MainWindow mainWindow,
        Action<bool> setDarkMode
    )
    {
        LinuxFileInfoProvider fileInfoProvider = new(LinuxShellHandler);
        LinuxFileIoHandler fileIoHandler = new();
        LocalFileSystem localFileSystem = new()
        {
            LocalFileInfoProvider = fileInfoProvider,
            IconProvider = new LinuxIconProvider(LinuxShellHandler),
            ArchiveManager = new LocalArchiveManager(fileInfoProvider, fileIoHandler),
            FileIoHandler = fileIoHandler,
            BinInteraction = new LinuxBinInteraction(LinuxShellHandler),
            FileProperties = new LinuxFileProperties(new LinuxPropertiesVmFactory()),
            LocalShellHandler = LinuxShellHandler,
            GitIntegration = new LocalGitIntegration(LinuxShellHandler),
        };

        IClipboard clipboard = mainWindow.Clipboard ?? throw new InvalidDataException();
        AvaloniaDialogService dialogService = new(mainWindow);
        ClipboardManager clipboardManager = new(
            clipboard,
            mainWindow.StorageProvider,
            localFileSystem
        );
        return new MainWindowViewModel(initialDir, localFileSystem, dialogService, setDarkMode)
        {
            SftpFsFactory = new SftpFileSystemFactory(dialogService),
            ClipboardManager = clipboardManager,
        };
    }

    public IDefaultSettingsProvider GetDefaultSettingsProvider() =>
        new LinuxDefaultSettingsProvider(LinuxShellHandler);
}
