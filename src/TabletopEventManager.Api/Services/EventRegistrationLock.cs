using System.Collections.Concurrent;

namespace TabletopEventManager.Api.Services;

/// <summary>
/// Serializes registration attempts per event so capacity/duplicate/cutoff checks and the
/// insert happen atomically. In-process only; a multi-instance deployment needs a
/// database-backed lock instead.
/// </summary>
public sealed class EventRegistrationLock
{
    private readonly ConcurrentDictionary<long, SemaphoreSlim> locks = new();

    public async Task<T> RunExclusiveAsync<T>(long eventId, Func<Task<T>> action, CancellationToken cancellationToken)
    {
        var semaphore = locks.GetOrAdd(eventId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            return await action();
        }
        finally
        {
            semaphore.Release();
        }
    }
}
