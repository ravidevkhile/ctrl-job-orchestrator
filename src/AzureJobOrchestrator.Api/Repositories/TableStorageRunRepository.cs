using Azure;
using Azure.Data.Tables;
using AzureJobOrchestrator.Core.Enums;
using AzureJobOrchestrator.Core.Models;

namespace AzureJobOrchestrator.Api.Repositories;

/// <summary>
/// Persists JobRun records in Azure Table Storage.
/// Partition key = JobDefinitionId, Row key = run Id (reversed tick for
/// newest-first ordering via lexicographic sort).
/// </summary>
public sealed class TableStorageRunRepository(TableServiceClient tableService, ILogger<TableStorageRunRepository> logger)
    : IRunRepository
{
    private const string TableName = "JobRuns";
    private TableClient? _table;

    private async Task<TableClient> GetTableAsync(CancellationToken ct)
    {
        if (_table is not null) return _table;
        _table = tableService.GetTableClient(TableName);
        await _table.CreateIfNotExistsAsync(ct);
        return _table;
    }

    public async Task<JobRun?> GetAsync(string runId, CancellationToken ct = default)
    {
        var table = await GetTableAsync(ct);
        // Row key is the run id; we don't know the partition, do a cross-partition query.
        await foreach (var entity in table.QueryAsync<TableEntity>(e => e.RowKey == runId, cancellationToken: ct))
        {
            return FromEntity(entity);
        }
        return null;
    }

    public async Task<IReadOnlyList<JobRun>> ListByJobAsync(string jobId, int maxItems = 50, CancellationToken ct = default)
    {
        var table = await GetTableAsync(ct);
        var results = new List<JobRun>();
        await foreach (var entity in table.QueryAsync<TableEntity>(e => e.PartitionKey == jobId, cancellationToken: ct))
        {
            results.Add(FromEntity(entity));
            if (results.Count >= maxItems) break;
        }
        // Newest first
        results.Sort((a, b) => b.StartedAt.CompareTo(a.StartedAt));
        return results;
    }

    public async Task UpsertAsync(JobRun run, CancellationToken ct = default)
    {
        var table = await GetTableAsync(ct);
        var entity = ToEntity(run);
        await table.UpsertEntityAsync(entity, TableUpdateMode.Replace, ct);
        logger.LogDebug("Upserted run {RunId} status={Status}", run.Id, run.Status);
    }

    private static TableEntity ToEntity(JobRun r) => new(r.JobDefinitionId, r.Id)
    {
        ["Target"] = r.Target.ToString(),
        ["Status"] = r.Status.ToString(),
        ["TriggeredBy"] = r.TriggeredBy.ToString(),
        ["StartedByUser"] = r.StartedByUser,
        ["StartedAt"] = r.StartedAt,
        ["CompletedAt"] = r.CompletedAt,
        ["ExternalId"] = r.ExternalId,
        ["OutputLocation"] = r.OutputLocation,
        ["Log"] = r.Log,
        ["ErrorMessage"] = r.ErrorMessage,
        ["ParametersJson"] = r.ParametersJson,
        ["JobType"] = r.JobType,
        ["PipelineRunId"] = r.PipelineRunId,
        ["PipelineStepIndex"] = r.PipelineStepIndex,
        ["StructuredOutputJson"] = r.StructuredOutputJson
    };

    private static JobRun FromEntity(TableEntity e) => new()
    {
        Id = e.RowKey,
        JobDefinitionId = e.PartitionKey,
        Target = Enum.Parse<JobTarget>(e.GetString("Target") ?? "Batch"),
        Status = Enum.Parse<JobStatus>(e.GetString("Status") ?? "Pending"),
        TriggeredBy = Enum.Parse<TriggerType>(e.GetString("TriggeredBy") ?? "Manual"),
        StartedByUser = e.GetString("StartedByUser") ?? "system",
        StartedAt = e.GetDateTimeOffset("StartedAt") ?? DateTimeOffset.UtcNow,
        CompletedAt = e.GetDateTimeOffset("CompletedAt"),
        ExternalId = e.GetString("ExternalId"),
        OutputLocation = e.GetString("OutputLocation"),
        Log = e.GetString("Log"),
        ErrorMessage = e.GetString("ErrorMessage"),
        ParametersJson = e.GetString("ParametersJson") ?? "{}",
        JobType = e.GetString("JobType") ?? string.Empty,
        PipelineRunId = e.GetString("PipelineRunId"),
        PipelineStepIndex = e.GetInt32("PipelineStepIndex"),
        StructuredOutputJson = e.GetString("StructuredOutputJson")
    };
}
