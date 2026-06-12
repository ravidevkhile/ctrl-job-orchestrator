using AzureJobOrchestrator.Core.Enums;

namespace AzureJobOrchestrator.Core.Models;

public class JobDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;

    /// <summary>Execution target — Azure Batch or Azure Functions.</summary>
    public JobTarget Target { get; set; }

    /// <summary>Template key that selects the worker/function to run, e.g. "DataValidation".</summary>
    public string JobType { get; set; } = string.Empty;

    /// <summary>Free-form JSON that is forwarded to the executor unchanged.</summary>
    public string ParametersJson { get; set; } = "{}";

    /// <summary>Optional cron expression for scheduled execution.</summary>
    public string? CronSchedule { get; set; }

    public bool Enabled { get; set; } = true;

    public string CreatedBy { get; set; } = "system";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Denormalised last-run snapshot for the table view — kept in sync by JobRunnerService.
    public string? LastRunId { get; set; }
    public JobStatus? LastRunStatus { get; set; }
    public DateTimeOffset? LastRunAt { get; set; }

    /// <summary>How this job gets triggered. Defaults to Manual.</summary>
    public TriggerConfig TriggerConfig { get; set; } = new();

    /// <summary>Template type that determines the executor and parameter schema.</summary>
    public string TemplateType { get; set; } = "DataValidation";
}
