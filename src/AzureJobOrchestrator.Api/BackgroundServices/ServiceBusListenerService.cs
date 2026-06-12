using AzureJobOrchestrator.Api.Services;
using AzureJobOrchestrator.Core.Enums;

namespace AzureJobOrchestrator.Api.BackgroundServices;

/// <summary>
/// Reads messages from the in-memory Service Bus simulation queue and
/// dispatches them to JobRunnerService or PipelineRunnerService.
///
/// In a real Azure deployment this work is done by the Azure Functions
/// Service Bus trigger (ServiceBusTriggerFunction), which calls the API's
/// /jobs/{id}/run or /pipelines/{id}/run endpoint.
/// </summary>
public sealed class ServiceBusListenerService(
    IServiceScopeFactory scopeFactory,
    InMemoryServiceBusSender queue,         // resolved as the concrete type to access the Channel
    ILogger<ServiceBusListenerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("ServiceBusListenerService started (in-memory simulation).");

        await foreach (var message in queue.Queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                logger.LogInformation("[SB] Received message {MsgId} action={Action} target={Target}",
                    message.MessageId, message.Action, message.TargetId);

                using var scope = scopeFactory.CreateScope();

                switch (message.Action)
                {
                    case "TriggerJob":
                    {
                        var runner = scope.ServiceProvider.GetRequiredService<JobRunnerService>();
                        await runner.TriggerAsync(message.TargetId, TriggerType.Manual, $"servicebus:{message.SentBy}", ct: stoppingToken);
                        break;
                    }
                    case "TriggerPipeline":
                    {
                        var runner = scope.ServiceProvider.GetRequiredService<PipelineRunnerService>();
                        await runner.TriggerAsync(message.TargetId, TriggerType.Manual,
                            $"servicebus:{message.SentBy}", message.MessageId, stoppingToken);
                        break;
                    }
                    default:
                        logger.LogWarning("[SB] Unknown action: {Action}", message.Action);
                        break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[SB] Failed to process message {MsgId}", message.MessageId);
            }
        }

        logger.LogInformation("ServiceBusListenerService stopped.");
    }
}
