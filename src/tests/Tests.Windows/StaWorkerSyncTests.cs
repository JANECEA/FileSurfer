using FileSurfer.Windows.Services.Shell;

namespace Tests.Windows;

public class StaWorkerSyncTests
{
    public static TheoryData<int, int, int> InvokeResultCases =>
        new()
        {
            { 1, 2, 3 },
            { -10, 5, -5 },
            { 0, 0, 0 },
        };

    [Theory]
    [MemberData(nameof(InvokeResultCases))]
    public void Invoke_ReturnsFunctionResult(int left, int right, int expected)
    {
        using StaWorkerSync worker = new("sta-worker-result");

        int result = worker.Invoke(() => left + right);

        Assert.Equal(expected, result);
    }

    public static TheoryData<string> WorkerNameCases =>
        new() { "sta-worker-alpha", "sta-worker-beta" };

    [Theory]
    [MemberData(nameof(WorkerNameCases))]
    public void Invoke_RunsOnStaBackgroundThreadWithConfiguredName(string workerName)
    {
        using StaWorkerSync worker = new(workerName);

        (string? Name, ApartmentState State, bool IsBackground) info = worker.Invoke(() =>
            (
                Thread.CurrentThread.Name,
                Thread.CurrentThread.GetApartmentState(),
                Thread.CurrentThread.IsBackground
            )
        );

        Assert.Equal(workerName, info.Name);
        Assert.Equal(ApartmentState.STA, info.State);
        Assert.True(info.IsBackground);
    }

    public static TheoryData<string> ExceptionMessageCases =>
        new() { "failure", "bad state", "operation aborted" };

    [Theory]
    [MemberData(nameof(ExceptionMessageCases))]
    public void Invoke_RethrowsExceptionFromWorker(string message)
    {
        using StaWorkerSync worker = new("sta-worker-exception");

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            worker.Invoke<int>(() => throw new InvalidOperationException(message))
        );

        Assert.Equal(message, ex.Message);
    }

    public static TheoryData<int> InvocationCountCases => new() { 1, 3, 8 };

    [Theory]
    [MemberData(nameof(InvocationCountCases))]
    public void Invoke_UsesSingleDedicatedThreadAcrossCalls(int count)
    {
        using StaWorkerSync worker = new("sta-worker-thread-id");
        HashSet<int> threadIds = [];

        for (int i = 0; i < count; i++)
            threadIds.Add(worker.Invoke(() => Environment.CurrentManagedThreadId));

        Assert.Single(threadIds);
    }

    public static TheoryData<int> InvokeAfterDisposeCases => new() { 0, 1 };

    [Theory]
    [MemberData(nameof(InvokeAfterDisposeCases))]
    public void Invoke_AfterDispose_Throws(int input)
    {
        StaWorkerSync worker = new("sta-worker-dispose");
        worker.Dispose();

        Assert.Throws<ObjectDisposedException>(() => worker.Invoke(() => input));
    }
}
