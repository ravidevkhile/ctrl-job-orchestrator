using AzureJobOrchestrator.Core.Models;

namespace AzureJobOrchestrator.Api.Services;

/// <summary>
/// Common contract for executing a job.  Implementations decide how to
/// actually run the work (Azure Batch task vs Azure Function invocation).
/// </summary>
public interface IJobExecutor
{
    /// <summary>
    /// Start the job asynchronously.
    /// Returns a platform-specific external id (Batch task id / Function invocation id).
    /// The run record is already persisted before this is called; the executor only
    /// needs to start the work and return the external handle.
    /// </summary>
    Task<string> StartAsync(JobDefinition job, JobRun run, CancellationToken ct = default);

    /// <summary>
    /// Poll the run for its current status.  Called by the background poller.
    /// Implementations should update <paramref name="run"/> in-place and return it.
    /// </summary>
    Task<JobRun> PollAsync(JobRun run, CancellationToken ct = default);
}
