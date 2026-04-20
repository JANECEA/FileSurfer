using Avalonia.Controls;
using FileSurfer.Core.ViewModels;

namespace FileSurfer.Core.Views;

/// <summary>
/// Hosts the UI for monitoring and controlling SFTP synchronization.
/// </summary>
public partial class SftpSynchronizerWindow : Window
{
    /// <summary>
    /// Initializes the SFTP synchronizer window and loads its XAML components.
    /// </summary>
    public SftpSynchronizerWindow() => InitializeComponent();

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        try
        {
            if (DataContext is SftpSynchronizerViewModel viewModel)
                await viewModel.DisposeAsync();
        }
        catch
        {
            // Disposing failed
        }
    }

    private void SyncEventsSizeChanged(object? sender, SizeChangedEventArgs e) =>
        SyncEventsViewer.ScrollToEnd();
}
