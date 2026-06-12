using AzureJobOrchestrator.Api.Services;
using AzureJobOrchestrator.Core.Models;
using System.Text.Json;
namespace AzureJobOrchestrator.Api.Services.Templates;
public sealed class BlobOperationExecutor(ILogger<BlobOperationExecutor> logger) : ITemplateExecutor
{
    public string TemplateType => "BlobOperation";
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);
    public async Task<TemplateResult> ExecuteAsync(JobDefinition job, JobRun run, CancellationToken ct = default)
    {
        var p = ParseParams(run.ParametersJson);
        var container = p.GetValueOrDefault("containerName")?.ToString() ?? "data";
        var folder = p.GetValueOrDefault("folderPath")?.ToString() ?? "/";
        var extension = p.GetValueOrDefault("fileExtension")?.ToString() ?? "*";
        var operation = p.GetValueOrDefault("operation")?.ToString() ?? "Archive";
        var retention = int.TryParse(p.GetValueOrDefault("retentionDays")?.ToString(), out var r) ? r : 30;
        var cutoff = DateTimeOffset.UtcNow.AddDays(-retention);
        await Task.Delay(1500, ct);  // simulate blob enumeration
        var filesProcessed = Random.Shared.Next(10, 500);
        var sizeGB = Math.Round(filesProcessed * Random.Shared.NextDouble() * 0.1, 2);
        var log = $"[BlobOperation] Operation: {operation}\n" +
                  $"Container: {container}{folder}\n" +
                  $"Filter: *.{extension}, older than {cutoff:yyyy-MM-dd}\n" +
                  $"Files processed: {filesProcessed}\n" +
                  $"Storage freed: {sizeGB} GB\n" +
                  $"Status: COMPLETED";
        var output = JsonSerializer.Serialize(new { container, operation, filesProcessed, sizeGbReleased = sizeGB, cutoffDate = cutoff }, _json);
        logger.LogInformation("[BlobOperation] {Op} in {Container}: {Files} files, {GB}GB", operation, container, filesProcessed, sizeGB);
        return new TemplateResult(true, log, output);
    }
    private static Dictionary<string, object?> ParseParams(string json)
    {
        try { return JsonSerializer.Deserialize<Dictionary<string, object?>>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? []; }
        catch { return []; }
    }
}
