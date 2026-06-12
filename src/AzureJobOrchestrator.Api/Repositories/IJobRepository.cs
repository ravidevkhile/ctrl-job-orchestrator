using AzureJobOrchestrator.Core.Enums;
using AzureJobOrchestrator.Core.Models;

namespace AzureJobOrchestrator.Api.Repositories;

public interface IJobRepository
{
    Task<IReadOnlyList<JobDefinition>> ListByTargetAsync(JobTarget target, CancellationToken ct = default);
    Task<JobDefinition?> GetAsync(string id, CancellationToken ct = default);
    Task UpsertAsync(JobDefinition job, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
    Task<bool> ExistsAsync(string id, CancellationToken ct = default);
}
