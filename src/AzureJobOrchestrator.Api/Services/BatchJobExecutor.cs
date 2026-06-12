using AzureJobOrchestrator.Core.Enums;
using AzureJobOrchestrator.Core.Models;
using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Auth;
using Microsoft.Azure.Batch.Common;

namespace AzureJobOrchestrator.Api.Services;

/// <summary>
/// Executes jobs on Azure Batch.
///
/// Real mode: ensures a pool exists, creates a Batch job + task, monitors
/// the task, and uploads stdout/stderr to Blob on completion.
///
/// Simulation mode (Batch:SimulationMode=true): mimics execution with
/// configurable delays so the UI flow can be demoed without a Batch account.
/// </summary>
public sealed class BatchJobExecutor(
    IConfiguration configuration,
    ILogger<BatchJobExecutor> logger) : IJobExecutor
{
    private bool SimulationMode => configuration.GetValue<bool>("Batch:SimulationMode", true);

    // ------------------------------------------------------------------ //
    //  Public interface
    // ------------------------------------------------------------------ //

    public async Task<string> StartAsync(JobDefinition job, JobRun run, CancellationToken ct = default)
    {
        if (SimulationMode)
            return await StartSimulatedAsync(job, run, ct);

        return await StartBatchTaskAsync(job, run, ct);
    }

    public async Task<JobRun> PollAsync(JobRun run, CancellationToken ct = default)
    {
        if (SimulationMode)
            return await PollSimulatedAsync(run, ct);

        return await PollBatchTaskAsync(run, ct);
    }

    // ------------------------------------------------------------------ //
    //  Simulation mode
    // ------------------------------------------------------------------ //

    private Task<string> StartSimulatedAsync(JobDefinition job, JobRun run, CancellationToken ct)
    {
        var externalId = $"sim-batch-{run.Id[..8]}";
        logger.LogInformation("[SIM] Started simulated Batch task {ExternalId} for job {JobName}", externalId, job.Name);
        // Store start time in ExternalId so PollSimulatedAsync can compute elapsed seconds.
        return Task.FromResult($"{externalId}|{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}");
    }

    private Task<JobRun> PollSimulatedAsync(JobRun run, CancellationToken ct)
    {
        // Parse the external id written by StartSimulatedAsync.
        var parts = (run.ExternalId ?? "|0").Split('|');
        if (long.TryParse(parts.ElementAtOrDefault(1), out var startEpoch))
        {
            var elapsed = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - startEpoch;
            var durationSeconds = configuration.GetValue<int>("Batch:SimulationDurationSeconds", 12);

            if (elapsed >= durationSeconds)
            {
                // Randomly fail 10 % of simulated runs to make the demo interesting.
                var shouldFail = Random.Shared.Next(0, 10) == 0;
                run.Status = shouldFail ? JobStatus.Failed : JobStatus.Succeeded;
                run.CompletedAt = DateTimeOffset.UtcNow;
                run.Log = shouldFail
                    ? "Simulated Batch task failed (random demo failure)."
                    : $"Simulated Batch task completed in {elapsed}s.\nProcessed 42 records successfully.";
                run.ErrorMessage = shouldFail ? "Exit code 1 (simulated)" : null;
                run.OutputLocation = shouldFail ? null : $"https://blob.example.com/outputs/{run.Id}/output.json";
                logger.LogInformation("[SIM] Run {RunId} → {Status}", run.Id, run.Status);
            }
            // else still Running — no change needed
        }
        return Task.FromResult(run);
    }

    // ------------------------------------------------------------------ //
    //  Real Azure Batch execution
    // ------------------------------------------------------------------ //

    private async Task<string> StartBatchTaskAsync(JobDefinition job, JobRun run, CancellationToken ct)
    {
        var accountName = configuration["Batch:AccountName"]
            ?? throw new InvalidOperationException("Batch:AccountName not configured.");
        var accountKey = configuration["Batch:AccountKey"]
            ?? throw new InvalidOperationException("Batch:AccountKey not configured.");
        var accountUrl = configuration["Batch:AccountUrl"]
            ?? throw new InvalidOperationException("Batch:AccountUrl not configured.");

        var poolId = configuration["Batch:PoolId"] ?? "orchestrator-pool";
        var batchJobId = $"job-{job.Id[..8]}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        var taskId = $"task-{run.Id[..8]}";

        // Worker executable published to Blob; downloaded to each node via resource files.
        var workerUrl = configuration["Batch:WorkerBlobUrl"]
            ?? throw new InvalidOperationException("Batch:WorkerBlobUrl not configured.");

        var credentials = new BatchSharedKeyCredentials(accountUrl, accountName, accountKey);
        using var batchClient = BatchClient.Open(credentials);

        // Ensure pool exists (auto-scale; creates if missing).
        await EnsurePoolAsync(batchClient, poolId, ct);

        // Create the Batch job.
        var batchJob = batchClient.JobOperations.CreateJob(batchJobId, new PoolInformation { PoolId = poolId });
        batchJob.DisplayName = $"{job.Name} — run {run.Id[..8]}";
        await batchJob.CommitAsync();

        // Add the task.
        var resourceFiles = new List<ResourceFile> { ResourceFile.FromUrl(workerUrl, "worker.exe") };
        var commandLine = $"./worker.exe --jobType {job.JobType} --runId {run.Id} --params '{job.ParametersJson.Replace("'", "\\'")}'";
        var task = new CloudTask(taskId, commandLine) { ResourceFiles = resourceFiles };
        await batchClient.JobOperations.AddTaskAsync(batchJobId, task);

        logger.LogInformation("Submitted Batch task {TaskId} in job {BatchJobId}", taskId, batchJobId);
        return $"{batchJobId}/{taskId}";
    }

    private async Task<JobRun> PollBatchTaskAsync(JobRun run, CancellationToken ct)
    {
        var accountName = configuration["Batch:AccountName"]!;
        var accountKey = configuration["Batch:AccountKey"]!;
        var accountUrl = configuration["Batch:AccountUrl"]!;

        if (run.ExternalId is null) return run;
        var parts = run.ExternalId.Split('/');
        if (parts.Length != 2) return run;
        var (batchJobId, taskId) = (parts[0], parts[1]);

        var credentials = new BatchSharedKeyCredentials(accountUrl, accountName, accountKey);
        using var batchClient = BatchClient.Open(credentials);

        var task = await batchClient.JobOperations.GetTaskAsync(batchJobId, taskId);

        if (task.State == TaskState.Completed)
        {
            run.CompletedAt = task.ExecutionInformation.EndTime;
            run.Status = task.ExecutionInformation.ExitCode == 0 ? JobStatus.Succeeded : JobStatus.Failed;
            var nodeFile = await task.GetNodeFileAsync("stdout.txt");
            run.Log = await TruncateAsync(nodeFile, 4096);
            if (run.Status == JobStatus.Failed)
                run.ErrorMessage = $"Exit code {task.ExecutionInformation.ExitCode}";
        }

        return run;
    }

    private static async Task<string> TruncateAsync(NodeFile file, int maxChars)
    {
        try
        {
            using var ms = new MemoryStream();
            await file.CopyToStreamAsync(ms);
            ms.Position = 0;
            using var reader = new StreamReader(ms);
            var text = await reader.ReadToEndAsync();
            return text.Length <= maxChars ? text : text[..maxChars] + "\n...[truncated]";
        }
        catch { return string.Empty; }
    }

    private static async Task EnsurePoolAsync(BatchClient client, string poolId, CancellationToken ct)
    {
        try
        {
            await client.PoolOperations.GetPoolAsync(poolId);
        }
        catch (BatchException ex) when (ex.RequestInformation.BatchError.Code == BatchErrorCodeStrings.PoolNotFound)
        {
            var pool = client.PoolOperations.CreatePool(
                poolId: poolId,
                targetDedicatedComputeNodes: 1,
                virtualMachineSize: "standard_d2s_v3",
                virtualMachineConfiguration: new VirtualMachineConfiguration(
                    new ImageReference("UbuntuServer", "Canonical", "22.04-LTS"),
                    "batch.node.ubuntu 22.04"));
            await pool.CommitAsync();
        }
    }
}
