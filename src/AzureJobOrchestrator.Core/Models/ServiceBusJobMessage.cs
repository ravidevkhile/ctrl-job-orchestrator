namespace AzureJobOrchestrator.Core.Models;

/// <summary>
/// Payload schema for messages sent to the Service Bus queue.
///
/// Supported actions:
///   "TriggerJob"      — trigger a single job by id
///   "TriggerPipeline" — trigger a full pipeline by id
/// </summary>
public class ServiceBusJobMessage
{
    public string MessageId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>"TriggerJob" | "TriggerPipeline"</summary>
    public string Action { get; set; } = "TriggerJob";

    /// <summary>JobDefinition.Id or PipelineDefinition.Id</summary>
    public string TargetId { get; set; } = string.Empty;

    /// <summary>Optional JSON that overrides / extends the target's ParametersJson.</summary>
    public string? PayloadJson { get; set; }

    public string SentBy { get; set; } = "ui";
    public DateTimeOffset EnqueuedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? Notes { get; set; }
}
