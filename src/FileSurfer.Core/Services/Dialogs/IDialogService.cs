using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace FileSurfer.Core.Services.Dialogs;

/// <summary>
/// Represents an asynchronous operation with no cancellation token or progress reporting.
/// </summary>
/// <typeparam name="T">
/// Result type produced by the operation.
/// </typeparam>
/// <returns>
/// A task that resolves to the operation result.
/// </returns>
public delegate Task<T> AsyncOperation<T>();

/// <summary>
/// Represents an asynchronous operation that supports cancellation.
/// </summary>
/// <typeparam name="T">
/// Result type produced by the operation.
/// </typeparam>
/// <param name="ct">
/// Cancellation token used to stop the operation.
/// </param>
/// <returns>
/// A task that resolves to the operation result.
/// </returns>
public delegate Task<T> CancellableOperation<T>(CancellationToken ct);

/// <summary>
/// Represents an asynchronous operation that supports progress reporting and cancellation.
/// </summary>
/// <typeparam name="T">
/// Result type produced by the operation.
/// </typeparam>
/// <param name="reporter">
/// Progress reporter used to emit status updates.
/// </param>
/// <param name="ct">
/// Cancellation token used to stop the operation.
/// </param>
/// <returns>
/// A task that resolves to the operation result.
/// </returns>
public delegate Task<T> ReportingOperation<T>(ProgressReporter reporter, CancellationToken ct);

/// <summary>
/// Defines dialog workflows for informational prompts, confirmations, input collection, and
/// execution wrappers for long-running asynchronous operations.
/// </summary>
[SuppressMessage(
    "ReSharper",
    "UnusedMember.Global",
    Justification = "Methods are included for parity."
)]
public interface IDialogService
{
    /// <summary>
    /// Shows a non-blocking informational dialog.
    /// </summary>
    /// <param name="title">
    /// Dialog window title.
    /// </param>
    /// <param name="info">
    /// Informational message text to display.
    /// </param>
    public void InfoDialog(string title, string info);

    /// <summary>
    /// Runs an asynchronous operation inside a blocking dialog.
    /// </summary>
    /// <typeparam name="T">
    /// Result type returned by the wrapped operation.
    /// </typeparam>
    /// <param name="title">
    /// Dialog window title.
    /// </param>
    /// <param name="operation">
    /// Operation to execute while the dialog remains active.
    /// </param>
    /// <returns>
    /// A task that resolves to the operation result.
    /// </returns>
    public Task<T> BlockingDialogAsync<T>(string title, AsyncOperation<T> operation);

    /// <summary>
    /// Runs a cancellable asynchronous operation inside a blocking dialog.
    /// </summary>
    /// <typeparam name="T">
    /// Result type returned by the wrapped operation.
    /// </typeparam>
    /// <param name="title">
    /// Dialog window title.
    /// </param>
    /// <param name="operation">
    /// Cancellable operation to execute while the dialog remains active.
    /// </param>
    /// <returns>
    /// A task that resolves to the operation result.
    /// </returns>
    public Task<T> BlockingDialogAsync<T>(string title, CancellableOperation<T> operation);

    /// <summary>
    /// Runs a progress-reporting, cancellable operation inside a blocking dialog.
    /// </summary>
    /// <typeparam name="T">
    /// Result type returned by the wrapped operation.
    /// </typeparam>
    /// <param name="title">
    /// Dialog window title.
    /// </param>
    /// <param name="operation">
    /// Operation that reports progress and supports cancellation.
    /// </param>
    /// <returns>
    /// A task that resolves to the operation result.
    /// </returns>
    public Task<T> BlockingDialogAsync<T>(string title, ReportingOperation<T> operation);

    /// <summary>
    /// Runs an asynchronous operation with a non-blocking progress dialog.
    /// </summary>
    /// <typeparam name="T">
    /// Result type returned by the wrapped operation.
    /// </typeparam>
    /// <param name="title">
    /// Dialog window title.
    /// </param>
    /// <param name="operation">
    /// Operation to execute in the background dialog context.
    /// </param>
    /// <returns>
    /// A task that resolves to the operation result.
    /// </returns>
    public Task<T> BackgroundDialogAsync<T>(string title, AsyncOperation<T> operation);

    /// <summary>
    /// Runs a cancellable asynchronous operation with a non-blocking progress dialog.
    /// </summary>
    /// <typeparam name="T">
    /// Result type returned by the wrapped operation.
    /// </typeparam>
    /// <param name="title">
    /// Dialog window title.
    /// </param>
    /// <param name="operation">
    /// Cancellable operation to execute in the background dialog context.
    /// </param>
    /// <returns>
    /// A task that resolves to the operation result.
    /// </returns>
    public Task<T> BackgroundDialogAsync<T>(string title, CancellableOperation<T> operation);

    /// <summary>
    /// Runs a progress-reporting, cancellable operation with a non-blocking progress dialog.
    /// </summary>
    /// <typeparam name="T">
    /// Result type returned by the wrapped operation.
    /// </typeparam>
    /// <param name="title">
    /// Dialog window title.
    /// </param>
    /// <param name="operation">
    /// Operation that reports progress and supports cancellation.
    /// </param>
    /// <returns>
    /// A task that resolves to the operation result.
    /// </returns>
    public Task<T> BackgroundDialogAsync<T>(string title, ReportingOperation<T> operation);

    /// <summary>
    /// Shows a confirmation dialog and returns the user's yes/no decision.
    /// </summary>
    /// <param name="title">
    /// Dialog window title.
    /// </param>
    /// <param name="question">
    /// Confirmation question shown to the user.
    /// </param>
    /// <returns>
    /// A task that returns <see langword="true"/> when confirmed, otherwise <see langword="false"/>.
    /// </returns>
    public Task<bool> ConfirmationDialogAsync(string title, string question);

    /// <summary>
    /// Shows an input dialog and returns user-entered text.
    /// </summary>
    /// <param name="title">
    /// Dialog window title.
    /// </param>
    /// <param name="context">
    /// Context message displayed above the input field.
    /// </param>
    /// <param name="secret">
    /// Indicates whether input should be masked (for sensitive values).
    /// </param>
    /// <returns>
    /// A task that returns the entered text, or <see langword="null"/> when the dialog is canceled.
    /// </returns>
    public Task<string?> InputDialogAsync(string title, string context, bool secret);

    /// <summary>
    /// Shows an input dialog with suggested options and returns user-entered or selected text.
    /// </summary>
    /// <param name="title">
    /// Dialog window title.
    /// </param>
    /// <param name="context">
    /// Context message displayed above the input field.
    /// </param>
    /// <param name="suggestionLabel">
    /// Label shown next to the suggestions list.
    /// </param>
    /// <param name="options">
    /// Suggested input values available for quick selection.
    /// </param>
    /// <returns>
    /// A task that returns the entered/selected text, or <see langword="null"/> when canceled.
    /// </returns>
    public Task<string?> SuggestInputDialogAsync(
        string title,
        string context,
        string suggestionLabel,
        IEnumerable<string> options
    );
}
