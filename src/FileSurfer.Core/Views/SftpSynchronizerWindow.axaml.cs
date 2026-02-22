using Avalonia.Controls;
using FileSurfer.Core.ViewModels;

namespace FileSurfer.Core.Views;

public partial class SftpSynchronizerWindow : Window
{
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
}
