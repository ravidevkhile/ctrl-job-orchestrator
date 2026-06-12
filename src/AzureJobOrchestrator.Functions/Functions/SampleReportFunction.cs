using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AzureJobOrchestrator.Functions.Functions;

/// <summary>
/// Demonstrates a "ReportGeneration" job type running as an Azure Function.
///
/// POST /api/ReportGeneration
/// Body: { "runId": "...", "jobDefinitionId": "...", "jobType": "ReportGeneration",
///          "parameters": { "template": "nightly", "to": ["..."], ... } }
/// </summary>
public sealed class SampleReportFunction(ILogger<SampleReportFunction> logger)
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    [Function("ReportGeneration")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "ReportGeneration")] HttpRequest req)
    {
        logger.LogInformation("ReportGeneration function triggered.");

        FunctionPayload? payload = null;
        try
        {
            payload = await JsonSerializer.DeserializeAsync<FunctionPayload>(req.Body, _json);
        }
        catch (JsonException)
        {
            return new BadRequestObjectResult(new { error = "Invalid JSON payload." });
        }

        if (payload is null)
            return new BadRequestObjectResult(new { error = "Empty request body." });

        var parameters = payload.Parameters;
        var template = parameters?.GetValueOrDefault("template")?.ToString() ?? "default";
        var format = parameters?.GetValueOrDefault("format")?.ToString() ?? "html";

        // Simulate report generation.
        await Task.Delay(800);

        var reportId = Guid.NewGuid().ToString("N")[..8];
        var outputUrl = $"https://blob.example.com/reports/{payload.RunId}/{reportId}.{format}";

        var result = new
        {
            runId = payload.RunId,
            reportId,
            template,
            format,
            outputUrl,
            pagesGenerated = Random.Shared.Next(5, 50),
            generatedAt = DateTimeOffset.UtcNow,
            message = $"Report '{template}' generated successfully as {format.ToUpperInvariant()}."
        };

        logger.LogInformation("ReportGeneration run {RunId} completed — report {ReportId}", payload.RunId, reportId);
        return new OkObjectResult(result);
    }

    private sealed record FunctionPayload(
        string RunId,
        string JobDefinitionId,
        string JobType,
        Dictionary<string, object?>? Parameters);
}
