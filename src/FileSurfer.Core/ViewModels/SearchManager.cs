using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using FileSurfer.Core.Models.FileInformation;

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

    private readonly FileSystemEntryVMFactory _entryVmFactory;
    private readonly IFileInfoProvider _fileInfoProvider;
    private readonly Action<string> _updateAnimation;
    private readonly Action<FileSystemEntryViewModel> _putEntry;
    private readonly DispatcherTimer _animationTimer;
    private readonly SemaphoreSlim _searchLock = new(1, 1);

    private CancellationTokenSource _searchCts = new();
    private Task<int?>? _activeSearchTask = null;

    public SearchManager(
        FileSystemEntryVMFactory entryVmFactory,
        IFileInfoProvider fileInfoProvider,
        Action<string> updateAnimation,
        Action<FileSystemEntryViewModel> putEntry
    )
    {
        _entryVmFactory = entryVmFactory;
        _fileInfoProvider = fileInfoProvider;
        _updateAnimation = updateAnimation;
        _putEntry = putEntry;
        _animationTimer = GetAnimationTimer();
    }

    public void CancelSearch()
    {
        CancellationTokenSource cts = _searchCts;
        if (!cts.IsCancellationRequested)
            cts.CancelAsync();
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

    public async Task<int?> SearchAsync(string searchQuery, IEnumerable<string> dirsToSearch)
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

            _activeSearchTask = SearchDirectoryAsync(dirsToSearch, searchQuery, token);
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
        IEnumerable<string> dirsToSearch,
        string searchQuery,
        CancellationToken token
    )
    {
        Queue<string> directories = new(dirsToSearch);

        int foundEntries = 0;
        while (directories.Count > 0 && !token.IsCancellationRequested)
        {
            string currentDirPath = directories.Dequeue();

            IEnumerable<string> dirPaths = GetAllDirs(currentDirPath);
            Task<List<FileSystemEntryViewModel>> filesTask = Task.Run(
                () => GetFiles(currentDirPath, searchQuery),
                _searchCts.Token
            );
            Task<List<FileSystemEntryViewModel>> filteredDirsTask = Task.Run(
                () => GetDirs(dirPaths, searchQuery),
                _searchCts.Token
            );
            await Task.WhenAll(filesTask, filteredDirsTask);

            foundEntries += filesTask.Result.Count + filteredDirsTask.Result.Count;
            await PushResults(filesTask.Result.Concat(filteredDirsTask.Result), token);

            foreach (string dirPath in dirPaths)
                directories.Enqueue(dirPath);
        }
        return token.IsCancellationRequested ? null : foundEntries;
    }

    private async Task PushResults(
        IEnumerable<FileSystemEntryViewModel> results,
        CancellationToken token
    )
    {
        foreach (FileSystemEntryViewModel[] batch in results.Chunk(ChunkSize))
        {
            if (token.IsCancellationRequested)
                return;

            try
            {
                await Dispatcher.UIThread.InvokeAsync(
                    () =>
                    {
                        foreach (FileSystemEntryViewModel item in batch)
                            if (!token.IsCancellationRequested)
                                _putEntry(item);
                    },
                    DispatcherPriority.Background,
                    token
                );
            }
            catch (OperationCanceledException)
            {
                return;
            }

            await Task.Yield();
        }
    }

    private List<FileSystemEntryViewModel> GetFiles(string directory, string query)
    {
        IEnumerable<string> filePaths = _fileInfoProvider.GetPathFiles(
            directory,
            FileSurferSettings.ShowHiddenFiles,
            FileSurferSettings.ShowProtectedFiles
        );
        return FilterPaths(filePaths, query)
            .Select(filePath => _entryVmFactory.File(filePath))
            .ToList();
    }

    private IEnumerable<string> GetAllDirs(string directory)
    {
        IEnumerable<string> dirPaths = _fileInfoProvider.GetPathDirs(
            directory,
            FileSurferSettings.ShowHiddenFiles,
            FileSurferSettings.ShowProtectedFiles
        );
        return dirPaths;
    }

    private List<FileSystemEntryViewModel> GetDirs(IEnumerable<string> dirs, string query) =>
        FilterPaths(dirs, query).Select(dirPath => _entryVmFactory.Directory(dirPath)).ToList();

    private static IEnumerable<string> FilterPaths(IEnumerable<string> paths, string query) =>
        paths.Where(path =>
            Path.GetFileName(path).Contains(query, StringComparison.CurrentCultureIgnoreCase)
        );

    public void Dispose()
    {
        _searchLock.Dispose();
        _searchCts.Dispose();
        _animationTimer.Stop();
    }
}
