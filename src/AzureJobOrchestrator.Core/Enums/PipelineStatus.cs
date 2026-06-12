namespace AzureJobOrchestrator.Core.Enums;

public enum PipelineStatus
{
    Pending,
    Running,
    Succeeded,
    Failed,
    Cancelled
}

public enum PipelineStepStatus
{
    Pending,
    Running,
    Succeeded,
    Failed,
    Skipped
}
