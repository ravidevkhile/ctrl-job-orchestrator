using AzureJobOrchestrator.Core.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace AzureJobOrchestrator.Functions.Functions;

/// <summary>
/// Listens to the "job-triggers" Service Bus queue (Azure deployment).
/// When a message arrives it calls back to the Orchestrator API to trigger
/// the specified job or pipeline.
///
/// In local simulation the API's in-memory ServiceBusSender + ServiceBusListenerService
/// handles this flow — this function is only active in Azure.
/// </summary>
public sealed class ServiceBusTriggerFunction(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<ServiceBusTriggerFunction> logger)
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    [Function("ServiceBusTrigger")]
    public async Task RunAsync(
        [ServiceBusTrigger("%ServiceBus:QueueName%", Connection = "ServiceBus:ConnectionString")]
        string messageBody)
    {
        logger.LogInformation("[SB Trigger] Received message: {Body}", messageBody[..Math.Min(200, messageBody.Length)]);

        ServiceBusJobMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<ServiceBusJobMessage>(messageBody, _json);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to deserialise Service Bus message.");
            return;
        }

        if (message is null) return;

        var apiBase = configuration["OrchestratorApi:BaseUrl"] ?? "http://localhost:5000";
        var client = httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(apiBase);

        string endpoint = message.Action switch
        {
            "TriggerJob" => $"/api/jobs/{message.TargetId}/run",
            "TriggerPipeline" => $"/api/pipelines/{message.TargetId}/run",
            _ => throw new InvalidOperationException($"Unknown action: {message.Action}")
        };

        var response = await client.PostAsync(endpoint, null);
        if (response.IsSuccessStatusCode)
            logger.LogInformation("[SB Trigger] {Action} on {Target} → {Status}", message.Action, message.TargetId, (int)response.StatusCode);
        else
            logger.LogWarning("[SB Trigger] {Action} on {Target} failed — HTTP {Status}", message.Action, message.TargetId, (int)response.StatusCode);
    }
}
