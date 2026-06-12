using AzureJobOrchestrator.Core.Models;
namespace AzureJobOrchestrator.Api.Services.Templates;
public interface ITemplateExecutor
{
    string TemplateType { get; }
    Task<TemplateResult> ExecuteAsync(JobDefinition job, JobRun run, CancellationToken ct = default);
}
public record TemplateResult(bool Success, string Log, string? StructuredOutputJson = null, string? ErrorMessage = null);
