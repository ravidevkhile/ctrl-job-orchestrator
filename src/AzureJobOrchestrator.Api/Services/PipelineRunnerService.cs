using AzureJobOrchestrator.Api.Repositories;
using AzureJobOrchestrator.Core.Enums;
using AzureJobOrchestrator.Core.Models;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AzureJobOrchestrator.Api.Services;

/// <summary>
/// Orchestrates pipeline execution:
///   • TriggerAsync   — creates a PipelineRun and starts step 0.
///   • OnStepCompleted — called by RunStatusPollerService when a job run
///                       that belongs to a pipeline step finishes.
///                       Advances to the next step (passing the output)
///                       or marks the pipeline Succeeded/Failed.
/// </summary>
public sealed class PipelineRunnerService(
    IPipelineRepository pipelineRepo,
    IPipelineRunRepository pipelineRunRepo,
    IJobRepository jobRepo,
    IRunRepository runRepo,
    JobRunnerService jobRunner,
    ILogger<PipelineRunnerService> logger)
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    // ------------------------------------------------------------------ //
    //  Trigger
    // ------------------------------------------------------------------ //

    public async Task<PipelineRun> TriggerAsync(
        string pipelineId,
        TriggerType trigger = TriggerType.Manual,
        string startedBy = "api",
        string? serviceBusMessageId = null,
        CancellationToken ct = default)
    {
        var pipeline = await pipelineRepo.GetAsync(pipelineId, ct)
            ?? throw new KeyNotFoundException($"Pipeline {pipelineId} not found.");

        if (!pipeline.Enabled)
            throw new InvalidOperationException($"Pipeline '{pipeline.Name}' is disabled.");

        if (pipeline.Steps.Count == 0)
            throw new InvalidOperationException($"Pipeline '{pipeline.Name}' has no steps.");

        // Build the run record with step stubs in Pending state.
        var pipelineRun = new PipelineRun
        {
            Id = Guid.NewGuid().ToString(),
            PipelineDefinitionId = pipeline.Id,
            PipelineName = pipeline.Name,
            Status = PipelineStatus.Running,
            CurrentStepIndex = 0,
            TotalSteps = pipeline.Steps.Count,
            StartedBy = startedBy,
            TriggeredBy = trigger,
            ServiceBusMessageId = serviceBusMessageId,
            StartedAt = DateTimeOffset.UtcNow,
            StepRuns = pipeline.Steps.OrderBy(s => s.Order).Select((s, i) => new PipelineStepRun
            {
                StepIndex = i,
                StepName = s.StepName,
                JobDefinitionId = s.JobDefinitionId,
                Status = i == 0 ? PipelineStepStatus.Running : PipelineStepStatus.Pending
            }).ToList()
        };

        await pipelineRunRepo.UpsertAsync(pipelineRun, ct);

        // Update pipeline last-run snapshot.
        pipeline.LastRunId = pipelineRun.Id;
        pipeline.LastRunStatus = PipelineStatus.Running;
        pipeline.LastRunAt = pipelineRun.StartedAt;
        await pipelineRepo.UpsertAsync(pipeline, ct);

        // Start step 0.
        await StartStepAsync(pipelineRun, pipeline.Steps[0], previousOutput: null, ct);

        logger.LogInformation("Pipeline '{Name}' started — run {RunId}", pipeline.Name, pipelineRun.Id);
        return pipelineRun;
    }

    // ------------------------------------------------------------------ //
    //  Called by RunStatusPollerService when a step's job run completes
    // ------------------------------------------------------------------ //

    public async Task OnStepCompletedAsync(JobRun completedRun, CancellationToken ct = default)
    {
        if (completedRun.PipelineRunId is null || completedRun.PipelineStepIndex is null) return;

        var pipelineRun = await pipelineRunRepo.GetAsync(completedRun.PipelineRunId, ct);
        if (pipelineRun is null) return;

        var pipeline = await pipelineRepo.GetAsync(pipelineRun.PipelineDefinitionId, ct);
        if (pipeline is null) return;

        var stepIndex = completedRun.PipelineStepIndex.Value;
        var stepRun = pipelineRun.StepRuns.FirstOrDefault(s => s.StepIndex == stepIndex);
        if (stepRun is null) return;

        // Update the completed step's record.
        stepRun.Status = completedRun.Status == JobStatus.Succeeded
            ? PipelineStepStatus.Succeeded
            : PipelineStepStatus.Failed;
        stepRun.CompletedAt = completedRun.CompletedAt;
        stepRun.ErrorMessage = completedRun.ErrorMessage;

        // Capture structured output from the completed run for passing forward.
        stepRun.OutputJson = BuildStepOutput(completedRun);

        if (completedRun.Status != JobStatus.Succeeded)
        {
            // Pipeline halts on step failure.
            pipelineRun.Status = PipelineStatus.Failed;
            pipelineRun.CompletedAt = DateTimeOffset.UtcNow;
            await pipelineRunRepo.UpsertAsync(pipelineRun, ct);
            await UpdatePipelineSnapshotAsync(pipeline, pipelineRun, ct);
            logger.LogWarning("Pipeline '{Name}' failed at step {Step}", pipeline.Name, stepIndex);
            return;
        }

        // Advance to the next step.
        var nextIndex = stepIndex + 1;
        if (nextIndex >= pipeline.Steps.Count)
        {
            // All steps done.
            pipelineRun.Status = PipelineStatus.Succeeded;
            pipelineRun.CompletedAt = DateTimeOffset.UtcNow;
            pipelineRun.CurrentStepIndex = nextIndex;
            await pipelineRunRepo.UpsertAsync(pipelineRun, ct);
            await UpdatePipelineSnapshotAsync(pipeline, pipelineRun, ct);
            logger.LogInformation("Pipeline '{Name}' completed successfully", pipeline.Name);
            return;
        }

        // Start next step with previous step's output.
        pipelineRun.CurrentStepIndex = nextIndex;
        pipelineRun.StepRuns[nextIndex].Status = PipelineStepStatus.Running;
        pipelineRun.StepRuns[nextIndex].StartedAt = DateTimeOffset.UtcNow;
        await pipelineRunRepo.UpsertAsync(pipelineRun, ct);

        var nextStep = pipeline.Steps.OrderBy(s => s.Order).ElementAt(nextIndex);
        await StartStepAsync(pipelineRun, nextStep, previousOutput: stepRun.OutputJson, ct);

        logger.LogInformation("Pipeline '{Name}' advancing to step {Next}", pipeline.Name, nextIndex);
    }

    // ------------------------------------------------------------------ //
    //  Helpers
    // ------------------------------------------------------------------ //

    private async Task StartStepAsync(
        PipelineRun pipelineRun,
        PipelineStep step,
        string? previousOutput,
        CancellationToken ct)
    {
        var job = await jobRepo.GetAsync(step.JobDefinitionId, ct)
            ?? throw new InvalidOperationException($"Pipeline step references missing job {step.JobDefinitionId}.");

        // Merge previous step output into this step's parameters.
        var mergedParams = MergeParameters(job.ParametersJson, previousOutput, step.OutputMappingJson);

        // Temporarily override the job's parameters for this run.
        var originalParams = job.ParametersJson;
        job.ParametersJson = mergedParams;

        var run = await jobRunner.TriggerAsync(
            job.Id, TriggerType.Scheduled, $"pipeline:{pipelineRun.PipelineName}", force: true, ct);

        job.ParametersJson = originalParams;

        // Link the job run back to the pipeline step.
        run.PipelineRunId = pipelineRun.Id;
        run.PipelineStepIndex = pipelineRun.CurrentStepIndex;
        run.ParametersJson = mergedParams;
        await runRepo.UpsertAsync(run, ct);

        // Record the job run id in the step.
        var stepRun = pipelineRun.StepRuns[pipelineRun.CurrentStepIndex];
        stepRun.JobRunId = run.Id;
        stepRun.StartedAt = run.StartedAt;
        await pipelineRunRepo.UpsertAsync(pipelineRun, ct);
    }

    private static string MergeParameters(string baseJson, string? previousOutput, string? mappingJson)
    {
        if (string.IsNullOrWhiteSpace(previousOutput)) return baseJson;

        try
        {
            var baseNode = JsonNode.Parse(baseJson) as JsonObject ?? [];
            var prevNode = JsonNode.Parse(previousOutput);

            if (mappingJson is { Length: > 0 })
            {
                // Apply a simple key mapping: { "myParam": "$.someKey" }
                var mapping = JsonSerializer.Deserialize<Dictionary<string, string>>(mappingJson, _json);
                if (mapping is not null && prevNode is JsonObject prevObj)
                {
                    foreach (var (destKey, sourcePath) in mapping)
                    {
                        var sourceKey = sourcePath.TrimStart('$', '.');
                        if (prevObj.TryGetPropertyValue(sourceKey, out var value))
                            baseNode[destKey] = value?.DeepClone();
                    }
                }
            }
            else
            {
                // Default: inject previous output under "previousStepOutput".
                baseNode["previousStepOutput"] = prevNode?.DeepClone();
            }

            return baseNode.ToJsonString();
        }
        catch
        {
            return baseJson;
        }
    }

    /// <summary>
    /// Builds a structured JSON summary of the completed run's output for passing
    /// to the next pipeline step.
    /// </summary>
    private static string BuildStepOutput(JobRun run)
    {
        var obj = new JsonObject
        {
            ["runId"] = run.Id,
            ["jobType"] = run.JobType,
            ["status"] = run.Status.ToString(),
            ["startedAt"] = run.StartedAt.ToString("O"),
            ["completedAt"] = run.CompletedAt?.ToString("O"),
            ["outputLocation"] = run.OutputLocation
        };

        // If the executor produced structured JSON, embed it directly.
        if (run.StructuredOutputJson is { Length: > 0 })
        {
            try { obj["result"] = JsonNode.Parse(run.StructuredOutputJson); } catch { /* ignore */ }
        }
        else if (run.Log is { Length: > 0 })
        {
            obj["log"] = run.Log.Length > 500 ? run.Log[..500] + "…" : run.Log;
        }

        return obj.ToJsonString();
    }

    private async Task UpdatePipelineSnapshotAsync(PipelineDefinition pipeline, PipelineRun run, CancellationToken ct)
    {
        pipeline.LastRunId = run.Id;
        pipeline.LastRunStatus = run.Status;
        pipeline.LastRunAt = run.CompletedAt ?? run.StartedAt;
        await pipelineRepo.UpsertAsync(pipeline, ct);
    }
}
