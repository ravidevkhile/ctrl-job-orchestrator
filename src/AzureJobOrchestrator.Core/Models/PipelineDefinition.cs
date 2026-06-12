namespace AzureJobOrchestrator.Core.Models;

/// <summary>
/// An ordered chain of jobs where each step's output is forwarded
/// as additional parameters to the next step.
/// </summary>
public class PipelineDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Ordered list of pipeline steps. Steps are executed sequentially;
    /// if a step fails the pipeline halts.
    /// </summary>
    public List<PipelineStep> Steps { get; set; } = [];

    public string CreatedBy { get; set; } = "system";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Denormalised last-run snapshot (kept in sync by PipelineRunnerService)
    public string? LastRunId { get; set; }
    public Enums.PipelineStatus? LastRunStatus { get; set; }
    public DateTimeOffset? LastRunAt { get; set; }
}

public class PipelineStep
{
    /// <summary>0-based execution order.</summary>
    public int Order { get; set; }
    public string StepName { get; set; } = string.Empty;
    public string JobDefinitionId { get; set; } = string.Empty;

    /// <summary>
    /// Optional JSON that describes how to extract values from the previous
    /// step's output and inject them into this step's parameters.
    /// Null → pass the entire previous output as { "previousStepOutput": {...} }.
    /// </summary>
    public string? OutputMappingJson { get; set; }
}
