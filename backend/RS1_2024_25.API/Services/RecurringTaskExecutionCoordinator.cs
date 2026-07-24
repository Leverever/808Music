using System.Collections.Concurrent;

namespace RS1_2024_25.API.Services;

public sealed class RecurringTaskExecutionCoordinator
{
    private readonly ConcurrentDictionary<string, byte> _runningTasks =
        new(StringComparer.OrdinalIgnoreCase);

    public bool TryBegin(string taskName)
    {
        return _runningTasks.TryAdd(taskName, 0);
    }

    public void End(string taskName)
    {
        _runningTasks.TryRemove(taskName, out _);
    }

    public bool IsRunning(string taskName)
    {
        return _runningTasks.ContainsKey(taskName);
    }
}
