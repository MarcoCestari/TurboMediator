using System.Collections.Concurrent;

namespace TurboMediator.Scheduling.Internal;

/// <summary>
/// Registry that holds all typed dispatch entries for recurring jobs.
/// Populated at startup by the <see cref="DependencyInjection.SchedulingBuilder"/> — zero reflection at runtime.
/// </summary>
internal sealed class JobDispatchRegistry
{
    private readonly ConcurrentDictionary<string, JobDispatchEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Registers a dispatch entry for a job.</summary>
    public void Register(JobDispatchEntry entry)
    {
        _entries[entry.JobId] = entry;
    }

    /// <summary>Gets the dispatch entry for a job ID.</summary>
    public JobDispatchEntry? GetEntry(string jobId)
    {
        _entries.TryGetValue(jobId, out var entry);
        return entry;
    }

    /// <summary>Gets all registered entries.</summary>
    public IReadOnlyCollection<JobDispatchEntry> GetAll() => _entries.Values.ToList();
}
