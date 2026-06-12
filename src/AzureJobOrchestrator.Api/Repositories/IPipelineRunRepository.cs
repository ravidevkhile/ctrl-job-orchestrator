using AzureJobOrchestrator.Core.Models;

namespace AzureJobOrchestrator.Api.Repositories;

public interface IPipelineRunRepository
{
    Task<PipelineRun?> GetAsync(string runId, CancellationToken ct = default);
    Task<IReadOnlyList<PipelineRun>> ListByPipelineAsync(string pipelineId, int max = 20, CancellationToken ct = default);
    Task UpsertAsync(PipelineRun run, CancellationToken ct = default);
}
