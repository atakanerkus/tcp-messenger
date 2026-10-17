namespace TcpMessenger.Tests.TestUtilities;

/// <summary>Small helpers that keep asynchronous tests free of ad-hoc timeouts and sleeps.</summary>
internal static class AsyncTestHelpers
{
    /// <summary>Upper bound for anything that happens on loopback. Generous for slow CI agents.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    /// <summary>A completion source that never runs continuations inline on the signalling thread.</summary>
    public static TaskCompletionSource<T> NewSignal<T>() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Awaits the task or fails the test with a clear message instead of hanging.</summary>
    public static async Task<T> WithinTimeout<T>(this Task<T> task, string what)
    {
        try
        {
            return await task.WaitAsync(Timeout);
        }
        catch (TimeoutException)
        {
            throw new TimeoutException($"Timed out after {Timeout.TotalSeconds:0} s while waiting for: {what}");
        }
    }

    /// <summary>Awaits the task or fails the test with a clear message instead of hanging.</summary>
    public static async Task WithinTimeout(this Task task, string what)
    {
        try
        {
            await task.WaitAsync(Timeout);
        }
        catch (TimeoutException)
        {
            throw new TimeoutException($"Timed out after {Timeout.TotalSeconds:0} s while waiting for: {what}");
        }
    }
}
