using AzureJobOrchestrator.Api.Services;
using AzureJobOrchestrator.Core.Models;
using System.Text.Json;
namespace AzureJobOrchestrator.Api.Services.Templates;
public sealed class StoredProcedureExecutor(ISecretProvider secretProvider, ILogger<StoredProcedureExecutor> logger) : ITemplateExecutor
{
    public string TemplateType => "StoredProcedure";
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);
    public async Task<TemplateResult> ExecuteAsync(JobDefinition job, JobRun run, CancellationToken ct = default)
    {
        var p = ParseParams(run.ParametersJson);
        var secretName = p.GetValueOrDefault("keyVaultSecretName")?.ToString();
        var spName = p.GetValueOrDefault("storedProcedureName")?.ToString() ?? "usp_Process";
        var timeout = int.TryParse(p.GetValueOrDefault("commandTimeoutSeconds")?.ToString(), out var t) ? t : 30;
        // Resolve connection string from Key Vault (or config)
        string? connStr = null;
        if (!string.IsNullOrWhiteSpace(secretName))
            connStr = await secretProvider.GetSecretAsync(secretName, ct);
        await Task.Delay(Math.Min(timeout * 100, 3000), ct);  // simulate execution
        var rowsAffected = Random.Shared.Next(0, 5000);
        var log = $"[StoredProcedure] Executing {spName}\n" +
                  $"Connection: {(connStr is not null ? "resolved from Key Vault" : "using default")}\n" +
                  $"Rows affected: {rowsAffected}\n" +
                  $"Execution time: {Random.Shared.Next(50, timeout * 1000)}ms\n" +
                  $"Status: SUCCESS";
        var output = JsonSerializer.Serialize(new { storedProcedure = spName, rowsAffected, executedAt = DateTimeOffset.UtcNow }, _json);
        logger.LogInformation("[StoredProcedure] {SpName} completed, {Rows} rows affected", spName, rowsAffected);
        return new TemplateResult(true, log, output);
    }
    private static Dictionary<string, object?> ParseParams(string json)
    {
        try { return JsonSerializer.Deserialize<Dictionary<string, object?>>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? []; }
        catch { return []; }
    }
}
