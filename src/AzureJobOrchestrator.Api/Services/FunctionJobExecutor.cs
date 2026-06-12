using AzureJobOrchestrator.Core.Enums;
using AzureJobOrchestrator.Core.Models;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace AzureJobOrchestrator.Api.Services;

/// <summary>
/// Executes jobs by invoking a corresponding Azure Function HTTP endpoint.
///
/// The mapping of JobType → function URL is read from configuration:
///   Functions:Endpoints:{JobType}  (e.g. Functions:Endpoints:DataValidation)
///
/// When no real function endpoint is configured the executor runs in
/// simulation mode automatically, mimicking execution with a delay.
/// </summary>
public sealed class FunctionJobExecutor(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<FunctionJobExecutor> logger) : IJobExecutor
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    // ------------------------------------------------------------------ //
    //  Public interface
    // ------------------------------------------------------------------ //

    public async Task<string> StartAsync(JobDefinition job, JobRun run, CancellationToken ct = default)
    {
        var endpoint = configuration[$"Functions:Endpoints:{job.JobType}"];
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            logger.LogWarning("No endpoint configured for job type {JobType}; running in simulation mode.", job.JobType);
            return await StartSimulatedAsync(job, run, ct);
        }

        return await InvokeFunctionAsync(endpoint, job, run, ct);
    }

    public async Task<JobRun> PollAsync(JobRun run, CancellationToken ct = default)
    {
        // Functions are invoked synchronously (HTTP wait).  If the ExternalId
        // starts with "sim-fn" we know it's a simulated run.
        if (run.ExternalId?.StartsWith("sim-fn") == true)
            return await PollSimulatedAsync(run, ct);

        // For real function invocations the result was already captured in StartAsync;
        // the run is terminal before PollAsync is ever called.
        return run;
    }

    // ------------------------------------------------------------------ //
    //  Simulation mode
    // ------------------------------------------------------------------ //

    private Task<string> StartSimulatedAsync(JobDefinition job, JobRun run, CancellationToken ct)
    {
        var id = $"sim-fn-{run.Id[..8]}|{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        logger.LogInformation("[SIM] Started simulated Function run {ExternalId} for job {JobName}", id, job.Name);
        return Task.FromResult(id);
    }

    private Task<JobRun> PollSimulatedAsync(JobRun run, CancellationToken ct)
    {
        var parts = (run.ExternalId ?? "|0").Split('|');
        if (long.TryParse(parts.ElementAtOrDefault(1), out var startEpoch))
        {
            var elapsed = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - startEpoch;
            var durationSeconds = configuration.GetValue<int>("Functions:SimulationDurationSeconds", 6);

            if (elapsed >= durationSeconds)
            {
                run.Status = JobStatus.Succeeded;
                run.CompletedAt = DateTimeOffset.UtcNow;
                run.Log = $"Function executed in {elapsed}s.\n" +
                          $"Job type: {run.JobType}\n" +
                          $"Result: 200 OK — processed successfully.";
                run.OutputLocation = $"https://blob.example.com/outputs/{run.Id}/result.json";
                logger.LogInformation("[SIM] Run {RunId} → Succeeded", run.Id);
            }
        }
        return Task.FromResult(run);
    }

    // ------------------------------------------------------------------ //
    //  Real function invocation
    // ------------------------------------------------------------------ //

    private async Task<string> InvokeFunctionAsync(string endpoint, JobDefinition job, JobRun run, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("FunctionClient");

        var payload = new
        {
            runId = run.Id,
            jobDefinitionId = job.Id,
            jobType = job.JobType,
            parameters = JsonSerializer.Deserialize<object>(run.ParametersJson, _json)
        };

        using var content = new StringContent(JsonSerializer.Serialize(payload, _json), Encoding.UTF8, "application/json");

        logger.LogInformation("Invoking function endpoint {Endpoint} for run {RunId}", endpoint, run.Id);

        using var response = await client.PostAsync(endpoint, content, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (response.IsSuccessStatusCode)
        {
            // The function responded synchronously — mark the run complete.
            run.Status = JobStatus.Succeeded;
            run.CompletedAt = DateTimeOffset.UtcNow;
            run.Log = $"Function returned {(int)response.StatusCode} {response.ReasonPhrase}\n\n{body}";
            logger.LogInformation("Function run {RunId} succeeded", run.Id);
        }
        else
        {
            run.Status = JobStatus.Failed;
            run.CompletedAt = DateTimeOffset.UtcNow;
            run.ErrorMessage = $"HTTP {(int)response.StatusCode}: {body[..Math.Min(500, body.Length)]}";
            logger.LogWarning("Function run {RunId} failed: {Error}", run.Id, run.ErrorMessage);
        }

        // Return the Azure Functions invocation id from the response header if present.
        var invocationId = response.Headers.TryGetValues("x-ms-invocation-id", out var ids)
            ? ids.FirstOrDefault() : null;
        return invocationId ?? $"fn-{run.Id[..8]}";
    }
}
