using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using FileSurfer.Core.Extensions;
using FileSurfer.Core.Models;
using FileSurfer.Core.Models.FileInformation;

namespace FileSurfer.Core.ViewModels;

public class SearchManager : IDisposable
{
    private const int ChunkSize = 50;
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

    private async Task WaitForActiveAsync()
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
            await WaitForActiveAsync();
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

    private CancellationToken ResetToken()
    {
        _searchCts.Dispose();
        _searchCts = new CancellationTokenSource();
        return _searchCts.Token;
    }

    public async Task<int?> SearchAsync(
        IFileSystem fileSystem,
        string searchQuery,
        string dirToSearch
    )
    {
        await _searchLock.WaitAsync();
        try
        {
            if (_activeSearchTask is not null)
                await WaitForActiveAsync();

            CancellationToken token = ResetToken();
            _updateAnimation(SearchingStates[0]);
            _animationTimer.Start();
            _activeSearchTask = SearchDirectoryAsync(fileSystem, searchQuery, dirToSearch, token);
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
        string dirToSearch,
        CancellationToken ct
    )
    {
        Queue<string> directories = new();
        directories.Enqueue(dirToSearch);

        int foundEntries = 0;
        while (directories.Count > 0 && !ct.IsCancellationRequested)
        {
            string currentDirPath = directories.Dequeue();

            DirectoryContents entries = await GetAllEntriesAsync(fileSystem, currentDirPath, ct);
            Task<List<FileSystemEntryViewModel>> filesTask = Task.Run(
                () => GetFiles(fileSystem, entries.Files, searchQuery),
                _searchCts.Token
            );
            Task<List<FileSystemEntryViewModel>> dirsTask = Task.Run(
                () => GetDirs(fileSystem, entries.Dirs, searchQuery),
                _searchCts.Token
            );
            await Task.WhenAll(filesTask, dirsTask);

            foundEntries += filesTask.Result.Count + dirsTask.Result.Count;
            await PushResultsAsync(filesTask.Result.Concat(dirsTask.Result), ct);

            foreach (DirectoryEntryInfo dir in entries.Dirs)
                directories.Enqueue(dir.PathToEntry);
        }
        return ct.IsCancellationRequested ? null : foundEntries;
    }

    private async Task PushResultsAsync(
        IEnumerable<FileSystemEntryViewModel> results,
        CancellationToken token
    )
    {
        FileSystemEntryViewModel[] buffer = new FileSystemEntryViewModel[ChunkSize];
        foreach (int added in results.EfficientChunk(buffer))
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

    private static async Task<DirectoryContents> GetAllEntriesAsync(
        IFileSystem fileSystem,
        string directory,
        CancellationToken ct
    )
    {
        ValueResult<DirectoryContents> result =
            await fileSystem.FileInfoProvider.GetPathEntriesAsync(
                directory,
                FileSurferSettings.ShowHiddenFiles,
                FileSurferSettings.ShowProtectedFiles,
                ct
            );

        return result.IsOk ? result.Value : new DirectoryContents { Files = [], Dirs = [] };
    }

    private static List<FileSystemEntryViewModel> GetFiles(
        IFileSystem fileSystem,
        IReadOnlyList<FileEntryInfo> files,
        string query
    ) =>
        FilterPaths(files, query)
            .Select(fileInfo => new FileSystemEntryViewModel(fileSystem, fileInfo))
            .ToList();

    private static List<FileSystemEntryViewModel> GetDirs(
        IFileSystem fileSystem,
        IReadOnlyList<DirectoryEntryInfo> dirs,
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
