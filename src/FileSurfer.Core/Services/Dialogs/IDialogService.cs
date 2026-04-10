using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FileSurfer.Core.Services.Dialogs;

public delegate Task<T> AsyncOperation<T>();
public delegate Task<T> CancellableOperation<T>(CancellationToken ct);
public delegate Task<T> ReportingOperation<T>(ProgressReporter reporter, CancellationToken ct);

public interface IDialogService
{
    public void InfoDialog(string title, string info);

    public Task<T> BlockingDialogAsync<T>(string title, AsyncOperation<T> operation);

    public Task<T> BlockingDialogAsync<T>(string title, CancellableOperation<T> operation);

    public Task<T> BlockingDialogAsync<T>(string title, ReportingOperation<T> operation);

    public Task<T> BackgroundDialogAsync<T>(string title, AsyncOperation<T> operation);

    public Task<T> BackgroundDialogAsync<T>(string title, CancellableOperation<T> operation);

    public Task<T> BackgroundDialogAsync<T>(string title, ReportingOperation<T> operation);

    public Task<bool> ConfirmationDialogAsync(string title, string question);

    public Task<string?> InputDialogAsync(string title, string context, bool secret);

    public Task<string?> SuggestInputDialogAsync(
        string title,
        string context,
        string suggestionLabel,
        IEnumerable<string> options
    );
}
