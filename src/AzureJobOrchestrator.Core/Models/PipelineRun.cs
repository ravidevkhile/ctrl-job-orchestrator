using AzureJobOrchestrator.Core.Enums;

namespace AzureJobOrchestrator.Core.Models;

/// <summary>
/// An execution instance of a PipelineDefinition.
/// Tracks which step is active and the result of each completed step.
/// </summary>
public class PipelineRun
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string PipelineDefinitionId { get; set; } = string.Empty;
    public string PipelineName { get; set; } = string.Empty;
    public PipelineStatus Status { get; set; } = PipelineStatus.Pending;
    public int CurrentStepIndex { get; set; } = 0;
    public int TotalSteps { get; set; }
    public string StartedBy { get; set; } = "system";
    public TriggerType TriggeredBy { get; set; } = TriggerType.Manual;

    /// <summary>Service Bus message id if this run was triggered by a message.</summary>
    public string? ServiceBusMessageId { get; set; }

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Ordered step execution records — serialised as JSON in Table Storage.</summary>
    public List<PipelineStepRun> StepRuns { get; set; } = [];
}

public class PipelineStepRun
{
    public int StepIndex { get; set; }
    public string StepName { get; set; } = string.Empty;
    public string JobDefinitionId { get; set; } = string.Empty;
    public string? JobRunId { get; set; }
    public PipelineStepStatus Status { get; set; } = PipelineStepStatus.Pending;

    /// <summary>
    /// Structured output captured from the job run.
    /// This is passed as { "previousStepOutput": ... } to the next step.
    /// </summary>
    public string? OutputJson { get; set; }

    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
}
