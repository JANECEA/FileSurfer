using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Interactivity;
using FileSurfer.Core.Services.Dialogs;
using ReactiveUI;

namespace FileSurfer.Core.Views.Dialogs;

/// <summary>
/// Displays progress details and optional cancellation controls for long-running operations.
/// </summary>
public partial class ProgressDialogWindow : Window
{
    /// <summary>
    /// Gets the progress reporter used as the dialog data source.
    /// </summary>
    public required ProgressReporter Reporter { get; init; }

    /// <summary>
    /// Gets the cancellation token source used to cancel the active operation.
    /// </summary>
    public required CancellationTokenSource? Cts { get; init; }

    /// <summary>
    /// Initializes the progress dialog window and loads its XAML components.
    /// </summary>
    public ProgressDialogWindow() => InitializeComponent();

    protected override void OnOpened(EventArgs e) =>
        DataContext = new ProgressReporterVm(Reporter, Cts);

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (!e.IsProgrammatic && Cts is null)
            e.Cancel = true;
        else
        {
            base.OnClosing(e);
            Cancel();
        }
    }

    private void Cancel()
    {
        if (Cts is not null && !Cts.IsCancellationRequested)
            Cts.Cancel();
    }

    private void OnCancelClicked(object? sender = null, RoutedEventArgs? e = null) => Cancel();
}

/// <summary>
/// View model wrapper that exposes progress and cancellation state for dialog binding.
/// </summary>
[SuppressMessage(
    "ReSharper",
    "UnusedMember.Global",
    Justification = "Properties are used by the progress dialog."
)]
public sealed class ProgressReporterVm : ReactiveObject
{
    /// <summary>
    /// Gets or sets the current progress reporter state.
    /// </summary>
    public ProgressReporter Reporter
    {
        get => _reporter;
        set => this.RaiseAndSetIfChanged(ref _reporter, value);
    }
    private ProgressReporter _reporter = ProgressReporter.None;

    /// <summary>
    /// Gets or sets the cancellation token source used by the dialog.
    /// </summary>
    public CancellationTokenSource? Cts
    {
        get => _cts;
        set => this.RaiseAndSetIfChanged(ref _cts, value);
    }
    private CancellationTokenSource? _cts;

    /// <summary>
    /// Creates a progress dialog view model with progress and cancellation state.
    /// </summary>
    public ProgressReporterVm(ProgressReporter reporter, CancellationTokenSource? cts)
    {
        Reporter = reporter;
        Cts = cts;
    }
}
