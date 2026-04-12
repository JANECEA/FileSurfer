using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Interactivity;
using FileSurfer.Core.Services.Dialogs;
using ReactiveUI;

namespace FileSurfer.Core.Views.Dialogs;

public partial class ProgressDialogWindow : Window
{
    public required ProgressReporter Reporter { get; init; }
    public required CancellationTokenSource? Cts { get; init; }

    public ProgressDialogWindow() => InitializeComponent();

    protected override void OnOpened(EventArgs e) =>
        DataContext = new ProgressReporterVm(Reporter, Cts);

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        Cancel();
    }

    private void Cancel()
    {
        if (Cts is not null && !Cts.IsCancellationRequested)
            Cts.Cancel();
    }

    private void OnCancelClicked(object? sender = null, RoutedEventArgs? e = null) => Cancel();
}

[SuppressMessage(
    "ReSharper",
    "UnusedMember.Global",
    Justification = "Properties are used by the progress dialog."
)]
public sealed class ProgressReporterVm : ReactiveObject
{
    public ProgressReporter Reporter
    {
        get => _reporter;
        set => this.RaiseAndSetIfChanged(ref _reporter, value);
    }
    private ProgressReporter _reporter = ProgressReporter.None;

    public CancellationTokenSource? Cts
    {
        get => _cts;
        set => this.RaiseAndSetIfChanged(ref _cts, value);
    }
    private CancellationTokenSource? _cts;

    public ProgressReporterVm(ProgressReporter reporter, CancellationTokenSource? cts)
    {
        Reporter = reporter;
        Cts = cts;
    }
}
