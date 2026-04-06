using System.Diagnostics.CodeAnalysis;
using Avalonia.Threading;
using FileSurfer.Core.Views.Dialogs;
using ReactiveUI;

namespace FileSurfer.Core.Services.Dialogs;

/// <summary>
/// Reports operation progress information to the UI.
/// </summary>
[SuppressMessage(
    "ReSharper",
    "UnusedMember.Global",
    Justification = $"Data used by {nameof(ProgressDialogWindow)}"
)]
public sealed class ProgressReporter : ReactiveObject
{
    public static ProgressReporter None => new();

    private bool _isIndeterminate = true;
    public bool IsIndeterminate
    {
        get => _isIndeterminate;
        set => this.RaiseAndSetIfChanged(ref _isIndeterminate, value);
    }

    private double _fraction = 0.0;
    public double Fraction
    {
        get => _fraction;
        private set => this.RaiseAndSetIfChanged(ref _fraction, value);
    }

    private string? _currentItem;
    public string? CurrentItem
    {
        get => _currentItem;
        private set => this.RaiseAndSetIfChanged(ref _currentItem, value);
    }

    public void Report(double fraction, string? item = null)
    {
        Dispatcher.UIThread.Post(() =>
        {
            Fraction = fraction;
            CurrentItem = item;
        });
    }

    public void ReportIndeterminate(string? item = null) => Report(double.NaN, item);
}

public sealed class IndeterminateReporter
{
    private readonly ProgressReporter _progressReporter;

    public IndeterminateReporter(ProgressReporter progressReporter)
    {
        _progressReporter = progressReporter;
        _progressReporter.IsIndeterminate = true;
    }

    public void ReportItem(string? item) => _progressReporter.ReportIndeterminate(item);
}

public sealed class CountingReporter
{
    private readonly ProgressReporter _progressReporter;
    private readonly double _totalCount;
    private long _finishedCount = 0;

    public CountingReporter(ProgressReporter progressReporter, long totalCount)
    {
        _progressReporter = progressReporter;
        _totalCount = totalCount;
        _progressReporter.IsIndeterminate = false;
    }

    public void ReportItem(string? item = null)
    {
        _progressReporter.Report(_finishedCount / _totalCount, item);
        _finishedCount++;
    }
}
