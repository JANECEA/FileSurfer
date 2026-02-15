using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using FileSurfer.Core.Models;

namespace FileSurfer.Core.ViewModels;

public class SearchManager : IDisposable
{
    private const int ChunkSize = 25;
    private const int SearchAnimationPeriodMs = 500;
    private const string SearchingFinishedLabel = "Searching finished";
    private static readonly IReadOnlyList<string> SearchingStates =
    [
        "Searching",
        "Searching.",
        "Searching..",
        "Searching...",
    ];

    private readonly Action<string> _updateAnimation;
    private readonly Action<FileSystemEntryViewModel> _putEntry;
    private readonly DispatcherTimer _animationTimer;
    private readonly SemaphoreSlim _searchLock = new(1, 1);

    private CancellationTokenSource _searchCts = new();
    private Task<int?>? _activeSearchTask = null;

    public SearchManager(Action<string> updateAnimation, Action<FileSystemEntryViewModel> putEntry)
    {
        _updateAnimation = updateAnimation;
        _putEntry = putEntry;
        _animationTimer = GetAnimationTimer();
    }

    public void CancelSearch()
    {
        CancellationTokenSource cts = _searchCts;
        if (!cts.IsCancellationRequested)
            cts.Cancel();
    }

    private async Task WaitForActiveTask()
    {
        if (!_searchCts.IsCancellationRequested)
            await _searchCts.CancelAsync();

        if (_activeSearchTask is not null)
        {
            try
            {
                await _activeSearchTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }
    }

    public async Task CancelSearchAsync()
    {
        await _searchLock.WaitAsync();
        try
        {
            await WaitForActiveTask();
        }
        finally
        {
            _searchLock.Release();
        }
    }

    private DispatcherTimer GetAnimationTimer()
    {
        int index = 0;
        DispatcherTimer timer = new()
        {
            Interval = TimeSpan.FromMilliseconds(SearchAnimationPeriodMs),
        };

        timer.Tick += (_, _) =>
        {
            if (!_searchCts.IsCancellationRequested)
                _updateAnimation(SearchingStates[index = (index + 1) % SearchingStates.Count]);
        };
        return timer;
    }

    public async Task<int?> SearchAsync(
        IFileSystem fileSystem,
        string searchQuery,
        IEnumerable<string> dirsToSearch
    )
    {
        await _searchLock.WaitAsync();
        try
        {
            if (_activeSearchTask is not null)
                await WaitForActiveTask();

            _searchCts.Dispose();
            _searchCts = new CancellationTokenSource();
            CancellationToken token = _searchCts.Token;

            _updateAnimation(SearchingStates[0]);
            _animationTimer.Start();

            _activeSearchTask = SearchDirectoryAsync(fileSystem, searchQuery, dirsToSearch, token);
        }
        finally
        {
            _searchLock.Release();
        }

        int? foundEntries;
        try
        {
            foundEntries = await _activeSearchTask;
        }
        catch (OperationCanceledException)
        {
            foundEntries = null;
        }
        finally
        {
            await _searchLock.WaitAsync();
            try
            {
                _animationTimer.Stop();
                if (!_searchCts.IsCancellationRequested)
                    _updateAnimation(SearchingFinishedLabel);

                _activeSearchTask = null;
            }
            finally
            {
                _searchLock.Release();
            }
        }

        return foundEntries;
    }

    private async Task<int?> SearchDirectoryAsync(
        IFileSystem fileSystem,
        string searchQuery,
        IEnumerable<string> dirsToSearch,
        CancellationToken token
    )
    {
        Queue<string> directories = new(dirsToSearch);

        int foundEntries = 0;
        while (directories.Count > 0 && !token.IsCancellationRequested)
        {
            string currentDirPath = directories.Dequeue();

            List<DirectoryEntryInfo> dirs = GetAllDirs(fileSystem, currentDirPath);
            Task<List<FileSystemEntryViewModel>> filesTask = Task.Run(
                () => GetFiles(fileSystem, currentDirPath, searchQuery),
                _searchCts.Token
            );
            Task<List<FileSystemEntryViewModel>> filteredDirsTask = Task.Run(
                () => GetDirs(fileSystem, dirs, searchQuery),
                _searchCts.Token
            );
            await Task.WhenAll(filesTask, filteredDirsTask);

            foundEntries += filesTask.Result.Count + filteredDirsTask.Result.Count;
            await PushResults(filesTask.Result.Concat(filteredDirsTask.Result), token);

            foreach (DirectoryEntryInfo dir in dirs)
                directories.Enqueue(dir.PathToEntry);
        }
        return token.IsCancellationRequested ? null : foundEntries;
    }

    private async Task PushResults(
        IEnumerable<FileSystemEntryViewModel> results,
        CancellationToken token
    )
    {
        FileSystemEntryViewModel[] buffer = new FileSystemEntryViewModel[ChunkSize];
        foreach (int added in EfficientChunk(results, buffer))
        {
            if (token.IsCancellationRequested)
                return;

            try
            {
                await Dispatcher.UIThread.InvokeAsync(
                    () =>
                    {
                        for (int i = 0; i < added; i++)
                            if (!token.IsCancellationRequested)
                                _putEntry(buffer[i]);
                    },
                    DispatcherPriority.Background,
                    token
                );
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private static IEnumerable<int> EfficientChunk(
        IEnumerable<FileSystemEntryViewModel> source,
        FileSystemEntryViewModel[] buffer
    )
    {
        int index = 0;
        foreach (FileSystemEntryViewModel item in source)
        {
            buffer[index++] = item;

            if (index == buffer.Length)
            {
                yield return index;
                index = 0;
            }
        }
        if (index != 0)
            yield return index;
    }

    private static List<FileSystemEntryViewModel> GetFiles(
        IFileSystem fileSystem,
        string directory,
        string query
    )
    {
        ValueResult<List<FileEntryInfo>> result = fileSystem.FileInfoProvider.GetPathFiles(
            directory,
            FileSurferSettings.ShowHiddenFiles,
            FileSurferSettings.ShowProtectedFiles
        );
        if (!result.IsOk)
            return [];

        return FilterPaths(result.Value, query)
            .Select(fileInfo => new FileSystemEntryViewModel(fileSystem, fileInfo))
            .ToList();
    }

    private static List<DirectoryEntryInfo> GetAllDirs(IFileSystem fileSystem, string directory)
    {
        ValueResult<List<DirectoryEntryInfo>> result = fileSystem.FileInfoProvider.GetPathDirs(
            directory,
            FileSurferSettings.ShowHiddenFiles,
            FileSurferSettings.ShowProtectedFiles
        );
        return result.IsOk ? result.Value : [];
    }

    private static List<FileSystemEntryViewModel> GetDirs(
        IFileSystem fileSystem,
        List<DirectoryEntryInfo> dirs,
        string query
    ) =>
        FilterPaths(dirs, query)
            .Select(entry => new FileSystemEntryViewModel(fileSystem, entry))
            .ToList();

    private static IEnumerable<T> FilterPaths<T>(IEnumerable<T> entries, string query)
        where T : IFileSystemEntry =>
        entries.Where(entry =>
            entry.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)
        );

    public void Dispose()
    {
        _searchLock.Dispose();
        _searchCts.Cancel();
        _searchCts.Dispose();
        _animationTimer.Stop();
    }
}
