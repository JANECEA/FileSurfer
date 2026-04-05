using System;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Interactivity;
using FileSurfer.Core.Services.Dialogs;

namespace FileSurfer.Core.Views.Dialogs;

public partial class ProgressDialogWindow : Window
{
    public required ProgressReporter Reporter { get; init; }
    public required CancellationTokenSource Cts { get; init; }

    public ProgressDialogWindow() => InitializeComponent();

    protected override void OnOpened(EventArgs e) => DataContext = Reporter;

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        Cancel();
    }

    private void Cancel()
    {
        if (!Cts.IsCancellationRequested)
            OnCancelClicked();
    }

    private void OnCancelClicked(object? sender = null, RoutedEventArgs? e = null) => Cancel();
}
