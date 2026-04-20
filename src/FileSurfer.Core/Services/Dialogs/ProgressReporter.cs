using System.Diagnostics.CodeAnalysis;
using Avalonia.Threading;
using FileSurfer.Core.Views.Dialogs;
using ReactiveUI;

namespace FileSurfer.Core.Services.Dialogs;

/// <summary>
/// Reports operation progress information to the UI.
/// </summary>
[
    SuppressMessage(
        "ReSharper",
        "UnusedMember.Global",
        Justification = $"Data used by {nameof(ProgressDialogWindow)}"
    ),
    SuppressMessage("Performance", "CA1822:Mark members as static"),
]
public sealed class ProgressReporter : ReactiveObject
{
    /// <summary>
    /// Gets a reporter instance for operations that do not require visible progress.
    /// </summary>
    public static ProgressReporter None => new();

    /// <summary>
    /// Gets the normalized maximum progress value.
    /// </summary>
    public double Maximum => 1.0;

    /// <summary>
    /// Gets the normalized minimum progress value.
    /// </summary>
    public double Minimum => 0.0;

    /// <summary>
    /// Gets or sets whether progress should be displayed as indeterminate.
    /// </summary>
    public bool IsIndeterminate
    {
        get => _isIndeterminate;
        set => this.RaiseAndSetIfChanged(ref _isIndeterminate, value);
    }
    private bool _isIndeterminate = true;

    /// <summary>
    /// Gets the current normalized progress fraction.
    /// </summary>
    public double Fraction
    {
        get => _fraction;
        private set => this.RaiseAndSetIfChanged(ref _fraction, value);
    }
    private double _fraction = 0.0;

    /// <summary>
    /// Gets the label of the item currently being processed.
    /// </summary>
    public string? CurrentItem
    {
        get => _currentItem;
        private set => this.RaiseAndSetIfChanged(ref _currentItem, value);
    }
    private string? _currentItem;

    /// <summary>
    /// Reports a progress update to the UI thread.
    /// </summary>
    /// <param name="fraction">
    /// Normalized progress value, typically between <see cref="Minimum"/> and <see cref="Maximum"/>.
    /// </param>
    /// <param name="item">
    /// Optional label describing the current item being processed.
    /// </param>
    public void Report(double fraction, string? item = null)
    {
        Dispatcher.UIThread.Post(
            () =>
            {
                Fraction = fraction;
                CurrentItem = item;
            },
            DispatcherPriority.Background
        );
    }

    /// <summary>
    /// Reports progress in indeterminate mode with an optional current-item label.
    /// </summary>
    /// <param name="item">
    /// Optional label describing the current item being processed.
    /// </param>
    public void ReportIndeterminate(string? item = null) => Report(double.NaN, item);
}

/// <summary>
/// Convenience wrapper that forces indeterminate progress reporting.
/// </summary>
public sealed class IndeterminateReporter
{
    private readonly ProgressReporter _progressReporter;

    /// <summary>
    /// Initializes an indeterminate progress wrapper over an existing reporter.
    /// </summary>
    /// <param name="progressReporter">
    /// Underlying reporter to receive indeterminate updates.
    /// </param>
    public IndeterminateReporter(ProgressReporter progressReporter)
    {
        _progressReporter = progressReporter;
        _progressReporter.IsIndeterminate = true;
    }

    /// <summary>
    /// Reports an item label while keeping progress in indeterminate mode.
    /// </summary>
    /// <param name="item">
    /// Item description to surface in the UI.
    /// </param>
    public void ReportItem(string? item) => _progressReporter.ReportIndeterminate(item);
}

/// <summary>
/// Convenience wrapper that reports determinate progress based on completed-item counts.
/// </summary>
public sealed class CountingReporter
{
    private readonly ProgressReporter _progressReporter;
    private readonly double _totalCount;
    private long _finishedCount = 0;

    /// <summary>
    /// Initializes a counting progress wrapper with a fixed total item count.
    /// </summary>
    /// <param name="progressReporter">
    /// Underlying reporter to receive determinate progress updates.
    /// </param>
    /// <param name="totalCount">
    /// Total number of items expected.
    /// </param>
    public CountingReporter(ProgressReporter progressReporter, long totalCount)
    {
        _progressReporter = progressReporter;
        _totalCount = totalCount == 0 ? 1.0 : totalCount;
        _progressReporter.IsIndeterminate = false;
    }

    /// <summary>
    /// Reports the current item and advances the completed-item counter by one.
    /// </summary>
    /// <param name="item">
    /// Optional label describing the current item being processed.
    /// </param>
    public void ReportItem(string? item = null)
    {
        _progressReporter.Report(_finishedCount / _totalCount, item);
        _finishedCount++;
    }
}
