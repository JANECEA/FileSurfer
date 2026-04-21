using System;
using System.Collections.Concurrent;
using System.Threading;

namespace FileSurfer.Windows.Services.Shell;

/// <summary>
/// Executes queued work synchronously on a dedicated STA background thread.
/// </summary>
public sealed class StaWorkerSync : IDisposable
{
    private readonly BlockingCollection<Action> _queue = new();
    private readonly Thread _thread;

    /// <summary>
    /// Initializes a new STA worker thread with the provided thread name.
    /// </summary>
    /// <param name="name">The worker thread name.</param>
    public StaWorkerSync(string name)
    {
        _thread = new Thread(ThreadLoop) { IsBackground = true, Name = name };

        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    private void ThreadLoop()
    {
        foreach (Action work in _queue.GetConsumingEnumerable())
            work();
    }

    /// <summary>
    /// Invokes work on the STA worker thread and returns its result.
    /// </summary>
    /// <param name="func">The function to execute.</param>
    /// <returns>The function result.</returns>
    public T Invoke<T>(Func<T> func)
    {
        T result = default!;
        Exception? exception = null;
        ManualResetEventSlim done = new(false);

        _queue.Add(() =>
        {
            try
            {
                result = func();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            finally
            {
                // ReSharper disable once AccessToDisposedClosure  - will run before disposal
                done.Set();
            }
        });

        done.Wait();

        done.Dispose();
        if (exception != null)
            throw exception;

        return result;
    }

    public void Dispose()
    {
        _queue.CompleteAdding();
        _thread.Join();
        _queue.Dispose();
    }
}
