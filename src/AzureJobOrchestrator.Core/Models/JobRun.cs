using AzureJobOrchestrator.Core.Enums;

namespace AzureJobOrchestrator.Core.Models;

public class JobRun
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string JobDefinitionId { get; set; } = string.Empty;
    public JobTarget Target { get; set; }
    public JobStatus Status { get; set; } = JobStatus.Pending;
    public TriggerType TriggeredBy { get; set; }
    public string StartedByUser { get; set; } = "system";
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Azure Batch task id OR Azure Functions invocation id.</summary>
    public string? ExternalId { get; set; }

    /// <summary>Blob URL of the job output artifact.</summary>
    public string? OutputLocation { get; set; }

    /// <summary>Truncated stdout/stderr log or a link to the full log blob.</summary>
    public string? Log { get; set; }

    public string? ErrorMessage { get; set; }

    /// <summary>Parameters snapshot — copied from the JobDefinition at run time.</summary>
    public string ParametersJson { get; set; } = "{}";

    /// <summary>Job type snapshot — copied from the JobDefinition at run time.</summary>
    public string JobType { get; set; } = string.Empty;

    // ---- Pipeline linkage (null when run stands alone) -------------------

    /// <summary>PipelineRun.Id this run belongs to, or null.</summary>
    public string? PipelineRunId { get; set; }

    /// <summary>0-based index of this run's step within the pipeline.</summary>
    public int? PipelineStepIndex { get; set; }

    /// <summary>
    /// Structured JSON output produced by the job (parsed from Log by the executor).
    /// Forwarded to the next pipeline step as { "previousStepOutput": ... }.
    /// </summary>
    public string? StructuredOutputJson { get; set; }
}
