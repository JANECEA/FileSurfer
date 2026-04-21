using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace FileSurfer.Windows.Models.FileInformation;

/// <summary>
/// Processes icon extraction requests on a dedicated STA worker thread.
/// </summary>
internal sealed class IconWorker : IDisposable
{
    private sealed class IconRequest
    {
        public string Path { get; }
        public TaskCompletionSource<Bitmap?> Completion { get; }

        public IconRequest(string path)
        {
            Path = path;
            Completion = new TaskCompletionSource<Bitmap?>(
                TaskCreationOptions.RunContinuationsAsynchronously
            );
        }
    }

    private readonly BlockingCollection<IconRequest> _queue = new();
    private readonly Thread _thread;
    private bool _disposing = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="IconWorker"/> class.
    /// </summary>
    public IconWorker()
    {
        Thread thread = new(ThreadMain) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        _thread = thread;
    }

    /// <summary>
    /// Queues a file path for icon extraction.
    /// </summary>
    /// <param name="path">The file path to extract an icon for.</param>
    /// <returns>A task that resolves to the extracted icon, or <see langword="null"/> when unavailable.</returns>
    public Task<Bitmap?> Enqueue(string path)
    {
        if (_disposing || _queue.IsAddingCompleted)
            return Task.FromResult<Bitmap?>(null);

        IconRequest request = new(path);
        try
        {
            _queue.Add(request);
            return request.Completion.Task;
        }
        catch
        {
            return Task.FromResult<Bitmap?>(null);
        }
    }

    private void ThreadMain()
    {
        foreach (IconRequest request in _queue.GetConsumingEnumerable())
            try
            {
                Bitmap? icon = ExtractFileIcon(request.Path);
                request.Completion.SetResult(icon);
            }
            catch (Exception ex)
            {
                request.Completion.SetException(ex);
            }
    }

    private static Bitmap? ExtractFileIcon(string path)
    {
        try
        {
            using System.Drawing.Icon? icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
            if (icon is null)
                return null;

            using System.Drawing.Bitmap winBitmap = icon.ToBitmap();
            using MemoryStream stream = new();
            winBitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            stream.Position = 0;
            Bitmap bitmap = new(stream);
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        _disposing = true;
        _queue.CompleteAdding();
        _thread.Join();
        _queue.Dispose();
    }
}
