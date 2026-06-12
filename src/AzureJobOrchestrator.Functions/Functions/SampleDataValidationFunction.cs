using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AzureJobOrchestrator.Functions.Functions;

/// <summary>
/// Demonstrates a "DataValidation" job type running as an Azure Function.
///
/// POST /api/DataValidation
/// Body: { "runId": "...", "jobDefinitionId": "...", "jobType": "DataValidation",
///          "parameters": { "dataset": "...", "threshold": 0.95, ... } }
///
/// Returns: 200 OK with a structured result, or 400/500 on error.
/// The FunctionJobExecutor in the API project calls this endpoint, captures the
/// response, and marks the run Succeeded/Failed accordingly.
/// </summary>
public sealed class SampleDataValidationFunction(ILogger<SampleDataValidationFunction> logger)
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    [Function("DataValidation")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "DataValidation")] HttpRequest req)
    {
        logger.LogInformation("DataValidation function triggered.");

        FunctionPayload? payload = null;
        try
        {
            payload = await JsonSerializer.DeserializeAsync<FunctionPayload>(req.Body, _json);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Invalid JSON payload.");
            return new BadRequestObjectResult(new { error = "Invalid JSON payload." });
        }

        if (payload is null)
            return new BadRequestObjectResult(new { error = "Empty request body." });

        logger.LogInformation("Validating dataset for run {RunId}, job {JobId}", payload.RunId, payload.JobDefinitionId);

        // --- Simulated validation logic ---
        var parameters = payload.Parameters;
        var dataset = parameters?.GetValueOrDefault("dataset")?.ToString() ?? "unknown";
        var threshold = double.TryParse(parameters?.GetValueOrDefault("threshold")?.ToString(), out var t) ? t : 0.95;

        // Simulate some work.
        await Task.Delay(500);

        var recordsProcessed = Random.Shared.Next(1_000, 100_000);
        var validationScore = Math.Round(0.9 + Random.Shared.NextDouble() * 0.1, 4);
        var passed = validationScore >= threshold;

        var result = new
        {
            runId = payload.RunId,
            dataset,
            recordsProcessed,
            validationScore,
            threshold,
            passed,
            message = passed
                ? $"Validation passed — score {validationScore:P2} meets threshold {threshold:P2}."
                : $"Validation FAILED — score {validationScore:P2} is below threshold {threshold:P2}.",
            timestamp = DateTimeOffset.UtcNow
        };

        if (!passed)
        {
            logger.LogWarning("DataValidation run {RunId} failed validation.", payload.RunId);
            return new ObjectResult(result) { StatusCode = StatusCodes.Status422UnprocessableEntity };
        }

        logger.LogInformation("DataValidation run {RunId} passed.", payload.RunId);
        return new OkObjectResult(result);
    }

    private sealed record FunctionPayload(
        string RunId,
        string JobDefinitionId,
        string JobType,
        Dictionary<string, object?>? Parameters);
}
