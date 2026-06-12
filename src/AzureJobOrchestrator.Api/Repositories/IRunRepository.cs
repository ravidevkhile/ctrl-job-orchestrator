using AzureJobOrchestrator.Core.Models;

namespace AzureJobOrchestrator.Api.Repositories;

public interface IRunRepository
{
    Task<JobRun?> GetAsync(string runId, CancellationToken ct = default);
    Task<IReadOnlyList<JobRun>> ListByJobAsync(string jobId, int maxItems = 50, CancellationToken ct = default);
    Task UpsertAsync(JobRun run, CancellationToken ct = default);
}
