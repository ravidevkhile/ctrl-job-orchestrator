using Azure;
using Azure.Data.Tables;
using AzureJobOrchestrator.Core.Enums;
using AzureJobOrchestrator.Core.Models;
using System.Text.Json;

namespace AzureJobOrchestrator.Api.Repositories;

/// <summary>
/// Stores PipelineDefinition in Azure Table Storage.
/// PartitionKey = "pipeline", RowKey = id.
/// Steps are serialised as JSON (Table Storage does not support nested objects).
/// </summary>
public sealed class TableStoragePipelineRepository(TableServiceClient tableService, ILogger<TableStoragePipelineRepository> logger)
    : IPipelineRepository
{
    private const string TableName = "PipelineDefinitions";
    private const string PartitionKey = "pipeline";
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);
    private TableClient? _table;

    private async Task<TableClient> GetTableAsync(CancellationToken ct)
    {
        if (_table is not null) return _table;
        _table = tableService.GetTableClient(TableName);
        await _table.CreateIfNotExistsAsync(ct);
        return _table;
    }

    public async Task<IReadOnlyList<PipelineDefinition>> ListAsync(CancellationToken ct = default)
    {
        var table = await GetTableAsync(ct);
        var results = new List<PipelineDefinition>();
        await foreach (var e in table.QueryAsync<TableEntity>(e => e.PartitionKey == PartitionKey, cancellationToken: ct))
            results.Add(FromEntity(e));
        results.Sort((a, b) => b.CreatedAt.CompareTo(a.CreatedAt));
        return results;
    }

    public async Task<PipelineDefinition?> GetAsync(string id, CancellationToken ct = default)
    {
        var table = await GetTableAsync(ct);
        try
        {
            var r = await table.GetEntityAsync<TableEntity>(PartitionKey, id, cancellationToken: ct);
            return FromEntity(r.Value);
        }
        catch (RequestFailedException ex) when (ex.Status == 404) { return null; }
    }

    public async Task UpsertAsync(PipelineDefinition p, CancellationToken ct = default)
    {
        var table = await GetTableAsync(ct);
        await table.UpsertEntityAsync(ToEntity(p), TableUpdateMode.Replace, ct);
        logger.LogDebug("Upserted pipeline {PipelineId}", p.Id);
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        var table = await GetTableAsync(ct);
        await table.DeleteEntityAsync(PartitionKey, id, cancellationToken: ct);
    }

    public async Task<bool> ExistsAsync(string id, CancellationToken ct = default) =>
        await GetAsync(id, ct) is not null;

    private static TableEntity ToEntity(PipelineDefinition p) => new(PartitionKey, p.Id)
    {
        ["Name"] = p.Name,
        ["Description"] = p.Description,
        ["Enabled"] = p.Enabled,
        ["StepsJson"] = JsonSerializer.Serialize(p.Steps, _json),
        ["CreatedBy"] = p.CreatedBy,
        ["CreatedAt"] = p.CreatedAt,
        ["LastRunId"] = p.LastRunId,
        ["LastRunStatus"] = p.LastRunStatus?.ToString(),
        ["LastRunAt"] = p.LastRunAt
    };

    private static PipelineDefinition FromEntity(TableEntity e) => new()
    {
        Id = e.RowKey,
        Name = e.GetString("Name") ?? string.Empty,
        Description = e.GetString("Description") ?? string.Empty,
        Enabled = e.GetBoolean("Enabled") ?? true,
        Steps = JsonSerializer.Deserialize<List<PipelineStep>>(e.GetString("StepsJson") ?? "[]", _json) ?? [],
        CreatedBy = e.GetString("CreatedBy") ?? "system",
        CreatedAt = e.GetDateTimeOffset("CreatedAt") ?? DateTimeOffset.UtcNow,
        LastRunId = e.GetString("LastRunId"),
        LastRunStatus = e.GetString("LastRunStatus") is string s ? Enum.Parse<PipelineStatus>(s) : null,
        LastRunAt = e.GetDateTimeOffset("LastRunAt")
    };
}
