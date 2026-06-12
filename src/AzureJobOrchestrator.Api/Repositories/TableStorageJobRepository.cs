using Azure;
using Azure.Data.Tables;
using AzureJobOrchestrator.Core.Enums;
using AzureJobOrchestrator.Core.Models;
using System.Text.Json;

namespace AzureJobOrchestrator.Api.Repositories;

/// <summary>
/// Persists JobDefinition records in Azure Table Storage.
/// Partition key = Target (Batch|Function), Row key = job Id.
/// When running locally with Azurite the storage connection string in
/// appsettings.Development.json points to the emulator.
/// </summary>
public sealed class TableStorageJobRepository(TableServiceClient tableService, ILogger<TableStorageJobRepository> logger)
    : IJobRepository
{
    private const string TableName = "JobDefinitions";
    private TableClient? _table;

    private async Task<TableClient> GetTableAsync(CancellationToken ct)
    {
        if (_table is not null) return _table;
        _table = tableService.GetTableClient(TableName);
        await _table.CreateIfNotExistsAsync(ct);
        return _table;
    }

    public async Task<IReadOnlyList<JobDefinition>> ListByTargetAsync(JobTarget target, CancellationToken ct = default)
    {
        var table = await GetTableAsync(ct);
        var results = new List<JobDefinition>();
        await foreach (var entity in table.QueryAsync<TableEntity>(e => e.PartitionKey == target.ToString(), cancellationToken: ct))
        {
            results.Add(FromEntity(entity));
        }
        // Most recently created first
        results.Sort((a, b) => b.CreatedAt.CompareTo(a.CreatedAt));
        return results;
    }

    public async Task<JobDefinition?> GetAsync(string id, CancellationToken ct = default)
    {
        // We don't know the partition without a full scan; try both targets.
        foreach (var target in Enum.GetValues<JobTarget>())
        {
            var table = await GetTableAsync(ct);
            try
            {
                var response = await table.GetEntityAsync<TableEntity>(target.ToString(), id, cancellationToken: ct);
                return FromEntity(response.Value);
            }
            catch (RequestFailedException ex) when (ex.Status == 404) { }
        }
        return null;
    }

    public async Task UpsertAsync(JobDefinition job, CancellationToken ct = default)
    {
        var table = await GetTableAsync(ct);
        var entity = ToEntity(job);
        await table.UpsertEntityAsync(entity, TableUpdateMode.Replace, ct);
        logger.LogDebug("Upserted job {JobId} ({JobName})", job.Id, job.Name);
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        var job = await GetAsync(id, ct);
        if (job is null) return;
        var table = await GetTableAsync(ct);
        await table.DeleteEntityAsync(job.Target.ToString(), id, cancellationToken: ct);
        logger.LogInformation("Deleted job {JobId}", id);
    }

    public async Task<bool> ExistsAsync(string id, CancellationToken ct = default) =>
        await GetAsync(id, ct) is not null;

    private static TableEntity ToEntity(JobDefinition j) => new(j.Target.ToString(), j.Id)
    {
        ["Name"] = j.Name,
        ["JobType"] = j.JobType,
        ["Target"] = j.Target.ToString(),
        ["ParametersJson"] = j.ParametersJson,
        ["CronSchedule"] = j.CronSchedule,
        ["Enabled"] = j.Enabled,
        ["CreatedBy"] = j.CreatedBy,
        ["CreatedAt"] = j.CreatedAt,
        ["LastRunId"] = j.LastRunId,
        ["LastRunStatus"] = j.LastRunStatus?.ToString(),
        ["LastRunAt"] = j.LastRunAt,
        ["TriggerConfigJson"] = System.Text.Json.JsonSerializer.Serialize(j.TriggerConfig, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)),
        ["TemplateType"] = j.TemplateType
    };

    private static JobDefinition FromEntity(TableEntity e)
    {
        AzureJobOrchestrator.Core.Models.TriggerConfig triggerConfig = new();
        var triggerJson = e.GetString("TriggerConfigJson");
        if (!string.IsNullOrWhiteSpace(triggerJson))
        {
            try
            {
                triggerConfig = System.Text.Json.JsonSerializer.Deserialize<AzureJobOrchestrator.Core.Models.TriggerConfig>(
                    triggerJson,
                    new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))
                    ?? new();
            }
            catch { /* use default */ }
        }
        return new()
        {
            Id = e.RowKey,
            Name = e.GetString("Name") ?? string.Empty,
            Target = Enum.Parse<JobTarget>(e.GetString("Target") ?? "Batch"),
            JobType = e.GetString("JobType") ?? string.Empty,
            ParametersJson = e.GetString("ParametersJson") ?? "{}",
            CronSchedule = e.GetString("CronSchedule"),
            Enabled = e.GetBoolean("Enabled") ?? true,
            CreatedBy = e.GetString("CreatedBy") ?? "system",
            CreatedAt = e.GetDateTimeOffset("CreatedAt") ?? DateTimeOffset.UtcNow,
            LastRunId = e.GetString("LastRunId"),
            LastRunStatus = e.GetString("LastRunStatus") is string s ? Enum.Parse<JobStatus>(s) : null,
            LastRunAt = e.GetDateTimeOffset("LastRunAt"),
            TriggerConfig = triggerConfig,
            TemplateType = e.GetString("TemplateType") ?? "DataValidation"
        };
    }
}
