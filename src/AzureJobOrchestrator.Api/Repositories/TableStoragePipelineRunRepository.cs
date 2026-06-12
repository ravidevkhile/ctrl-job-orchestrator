using Azure;
using Azure.Data.Tables;
using AzureJobOrchestrator.Core.Enums;
using AzureJobOrchestrator.Core.Models;
using System.Text.Json;

namespace AzureJobOrchestrator.Api.Repositories;

/// <summary>
/// Stores PipelineRun in Azure Table Storage.
/// PartitionKey = pipelineDefinitionId, RowKey = runId.
/// StepRuns are serialised as JSON.
/// </summary>
public sealed class TableStoragePipelineRunRepository(TableServiceClient tableService, ILogger<TableStoragePipelineRunRepository> logger)
    : IPipelineRunRepository
{
    private const string TableName = "PipelineRuns";
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);
    private TableClient? _table;

    private async Task<TableClient> GetTableAsync(CancellationToken ct)
    {
        if (_table is not null) return _table;
        _table = tableService.GetTableClient(TableName);
        await _table.CreateIfNotExistsAsync(ct);
        return _table;
    }

    public async Task<PipelineRun?> GetAsync(string runId, CancellationToken ct = default)
    {
        var table = await GetTableAsync(ct);
        await foreach (var e in table.QueryAsync<TableEntity>(e => e.RowKey == runId, cancellationToken: ct))
            return FromEntity(e);
        return null;
    }

    public async Task<IReadOnlyList<PipelineRun>> ListByPipelineAsync(string pipelineId, int max = 20, CancellationToken ct = default)
    {
        var table = await GetTableAsync(ct);
        var results = new List<PipelineRun>();
        await foreach (var e in table.QueryAsync<TableEntity>(e => e.PartitionKey == pipelineId, cancellationToken: ct))
        {
            results.Add(FromEntity(e));
            if (results.Count >= max) break;
        }
        results.Sort((a, b) => b.StartedAt.CompareTo(a.StartedAt));
        return results;
    }

    public async Task UpsertAsync(PipelineRun run, CancellationToken ct = default)
    {
        var table = await GetTableAsync(ct);
        await table.UpsertEntityAsync(ToEntity(run), TableUpdateMode.Replace, ct);
        logger.LogDebug("Upserted pipeline run {RunId} status={Status} step={Step}", run.Id, run.Status, run.CurrentStepIndex);
    }

    private static TableEntity ToEntity(PipelineRun r) => new(r.PipelineDefinitionId, r.Id)
    {
        ["PipelineName"] = r.PipelineName,
        ["Status"] = r.Status.ToString(),
        ["CurrentStepIndex"] = r.CurrentStepIndex,
        ["TotalSteps"] = r.TotalSteps,
        ["StartedBy"] = r.StartedBy,
        ["TriggeredBy"] = r.TriggeredBy.ToString(),
        ["ServiceBusMessageId"] = r.ServiceBusMessageId,
        ["StartedAt"] = r.StartedAt,
        ["CompletedAt"] = r.CompletedAt,
        ["StepRunsJson"] = JsonSerializer.Serialize(r.StepRuns, _json)
    };

    private static PipelineRun FromEntity(TableEntity e) => new()
    {
        Id = e.RowKey,
        PipelineDefinitionId = e.PartitionKey,
        PipelineName = e.GetString("PipelineName") ?? string.Empty,
        Status = Enum.Parse<PipelineStatus>(e.GetString("Status") ?? "Pending"),
        CurrentStepIndex = e.GetInt32("CurrentStepIndex") ?? 0,
        TotalSteps = e.GetInt32("TotalSteps") ?? 0,
        StartedBy = e.GetString("StartedBy") ?? "system",
        TriggeredBy = Enum.Parse<TriggerType>(e.GetString("TriggeredBy") ?? "Manual"),
        ServiceBusMessageId = e.GetString("ServiceBusMessageId"),
        StartedAt = e.GetDateTimeOffset("StartedAt") ?? DateTimeOffset.UtcNow,
        CompletedAt = e.GetDateTimeOffset("CompletedAt"),
        StepRuns = JsonSerializer.Deserialize<List<PipelineStepRun>>(e.GetString("StepRunsJson") ?? "[]", _json) ?? []
    };
}
