using AzureJobOrchestrator.Api.Repositories;
using AzureJobOrchestrator.Core.Enums;
using AzureJobOrchestrator.Core.Models;

namespace AzureJobOrchestrator.Api.Services;

/// <summary>
/// Orchestrates the full run lifecycle:
///   1. Validate + load the job definition.
///   2. Create a pending JobRun and persist it.
///   3. Update the job's LastRun* denormalised fields.
///   4. Delegate to the appropriate IJobExecutor (Batch or Function).
///   5. Update the run to Running and re-persist.
///
/// Status polling is handled by RunStatusPollerService (background service).
/// </summary>
public sealed class JobRunnerService(
    IJobRepository jobRepo,
    IRunRepository runRepo,
    BatchJobExecutor batchExecutor,
    FunctionJobExecutor functionExecutor,
    ILogger<JobRunnerService> logger)
{
    public async Task<JobRun> TriggerAsync(
        string jobId,
        TriggerType trigger,
        string startedBy = "api",
        bool force = false,
        CancellationToken ct = default)
    {
        var job = await jobRepo.GetAsync(jobId, ct)
            ?? throw new KeyNotFoundException($"Job {jobId} not found.");

        if (!job.Enabled && !force)
            throw new InvalidOperationException($"Job '{job.Name}' is disabled. Pass force=true to override.");

        var run = new JobRun
        {
            Id = Guid.NewGuid().ToString(),
            JobDefinitionId = job.Id,
            Target = job.Target,
            Status = JobStatus.Pending,
            TriggeredBy = trigger,
            StartedByUser = startedBy,
            StartedAt = DateTimeOffset.UtcNow,
            ParametersJson = job.ParametersJson,
            JobType = job.JobType
        };

        // Persist the pending run and update the job's last-run snapshot.
        await runRepo.UpsertAsync(run, ct);
        job.LastRunId = run.Id;
        job.LastRunStatus = JobStatus.Pending;
        job.LastRunAt = run.StartedAt;
        await jobRepo.UpsertAsync(job, ct);

        // Start the job asynchronously.
        IJobExecutor executor = job.Target == JobTarget.Batch ? batchExecutor : functionExecutor;
        try
        {
            var externalId = await executor.StartAsync(job, run, ct);
            run.ExternalId = externalId;
            run.Status = JobStatus.Running;
            job.LastRunStatus = JobStatus.Running;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start job {JobId}", jobId);
            run.Status = JobStatus.Failed;
            run.CompletedAt = DateTimeOffset.UtcNow;
            run.ErrorMessage = ex.Message;
            job.LastRunStatus = JobStatus.Failed;
        }

        await runRepo.UpsertAsync(run, ct);
        await jobRepo.UpsertAsync(job, ct);

        logger.LogInformation("Job {JobName} triggered → run {RunId} ({Status})", job.Name, run.Id, run.Status);
        return run;
    }

    /// <summary>Reruns a previous run with the same parameters.</summary>
    public async Task<JobRun> RerunAsync(string runId, string startedBy = "api", CancellationToken ct = default)
    {
        var original = await runRepo.GetAsync(runId, ct)
            ?? throw new KeyNotFoundException($"Run {runId} not found.");

        // Patch parameters onto the job definition snapshot so StartAsync sees them.
        var job = await jobRepo.GetAsync(original.JobDefinitionId, ct)
            ?? throw new KeyNotFoundException($"Job {original.JobDefinitionId} not found.");

        // Temporarily use original parameters if they differ.
        var savedParams = job.ParametersJson;
        job.ParametersJson = original.ParametersJson;

        var run = new JobRun
        {
            Id = Guid.NewGuid().ToString(),
            JobDefinitionId = job.Id,
            Target = original.Target,
            Status = JobStatus.Pending,
            TriggeredBy = TriggerType.Rerun,
            StartedByUser = startedBy,
            StartedAt = DateTimeOffset.UtcNow,
            ParametersJson = original.ParametersJson,
            JobType = original.JobType
        };

        await runRepo.UpsertAsync(run, ct);
        job.LastRunId = run.Id;
        job.LastRunStatus = JobStatus.Pending;
        job.LastRunAt = run.StartedAt;
        await jobRepo.UpsertAsync(job, ct);

        IJobExecutor executor = job.Target == JobTarget.Batch ? batchExecutor : functionExecutor;
        try
        {
            run.ExternalId = await executor.StartAsync(job, run, ct);
            run.Status = JobStatus.Running;
            job.LastRunStatus = JobStatus.Running;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Rerun failed for original run {RunId}", runId);
            run.Status = JobStatus.Failed;
            run.CompletedAt = DateTimeOffset.UtcNow;
            run.ErrorMessage = ex.Message;
            job.LastRunStatus = JobStatus.Failed;
        }

        // Restore saved params.
        job.ParametersJson = savedParams;

        await runRepo.UpsertAsync(run, ct);
        await jobRepo.UpsertAsync(job, ct);

        return run;
    }

    public IJobExecutor GetExecutor(JobTarget target) =>
        target == JobTarget.Batch ? batchExecutor : functionExecutor;
}
