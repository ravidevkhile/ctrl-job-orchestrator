using AzureJobOrchestrator.Core.Models;

namespace AzureJobOrchestrator.Api.Repositories;

public interface IPipelineRepository
{
    Task<IReadOnlyList<PipelineDefinition>> ListAsync(CancellationToken ct = default);
    Task<PipelineDefinition?> GetAsync(string id, CancellationToken ct = default);
    Task UpsertAsync(PipelineDefinition pipeline, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
    Task<bool> ExistsAsync(string id, CancellationToken ct = default);
}
