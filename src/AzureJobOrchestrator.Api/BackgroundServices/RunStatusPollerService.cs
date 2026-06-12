using AzureJobOrchestrator.Api.Repositories;
using AzureJobOrchestrator.Api.Services;
using AzureJobOrchestrator.Core.Enums;
using AzureJobOrchestrator.Core.Models;

namespace AzureJobOrchestrator.Api.BackgroundServices;

/// <summary>
/// Polls active job runs every 5 seconds and advances their status.
/// When a run that belongs to a pipeline step reaches a terminal state,
/// it calls PipelineRunnerService.OnStepCompleted to trigger the next step.
/// </summary>
public sealed class RunStatusPollerService(
    IServiceScopeFactory scopeFactory,
    ILogger<RunStatusPollerService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("RunStatusPollerService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(PollInterval, stoppingToken);

            try { await PollActiveRunsAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Error during run status poll cycle."); }
        }

        logger.LogInformation("RunStatusPollerService stopped.");
    }

    private async Task PollActiveRunsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var runRepo = scope.ServiceProvider.GetRequiredService<IRunRepository>();
        var jobRepo = scope.ServiceProvider.GetRequiredService<IJobRepository>();
        var runner = scope.ServiceProvider.GetRequiredService<JobRunnerService>();
        var pipelineRunner = scope.ServiceProvider.GetRequiredService<PipelineRunnerService>();

        // Collect all active runs across every job.
        var activeRuns = new List<JobRun>();
        foreach (var target in Enum.GetValues<JobTarget>())
        {
            var jobs = await jobRepo.ListByTargetAsync(target, ct);
            foreach (var job in jobs)
            {
                if (job.LastRunId is null) continue;
                if (job.LastRunStatus is not (JobStatus.Pending or JobStatus.Running)) continue;
                var run = await runRepo.GetAsync(job.LastRunId, ct);
                if (run is { Status: JobStatus.Pending or JobStatus.Running })
                    activeRuns.Add(run);
            }
        }

        if (activeRuns.Count == 0) return;
        logger.LogDebug("Polling {Count} active run(s)…", activeRuns.Count);

        foreach (var run in activeRuns)
        {
            try
            {
                var executor = runner.GetExecutor(run.Target);
                var updated = await executor.PollAsync(run, ct);

                if (updated.Status == run.Status) continue;

                await runRepo.UpsertAsync(updated, ct);

                // Sync the denormalised last-run status on the job definition.
                var job = await jobRepo.GetAsync(updated.JobDefinitionId, ct);
                if (job is not null && job.LastRunId == updated.Id)
                {
                    job.LastRunStatus = updated.Status;
                    if (updated.CompletedAt.HasValue) job.LastRunAt = updated.CompletedAt;
                    await jobRepo.UpsertAsync(job, ct);
                }

                logger.LogInformation("Run {RunId} status → {Status}", updated.Id, updated.Status);

                // If this run is a pipeline step and just reached a terminal state,
                // notify the pipeline runner so it can advance (or fail) the pipeline.
                if (updated.PipelineRunId is not null &&
                    updated.Status is JobStatus.Succeeded or JobStatus.Failed)
                {
                    await pipelineRunner.OnStepCompletedAsync(updated, ct);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to poll run {RunId}", run.Id);
            }
        }
    }
}
