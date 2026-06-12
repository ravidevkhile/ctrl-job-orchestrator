using AzureJobOrchestrator.Core.Models;

namespace AzureJobOrchestrator.Api.Services;

/// <summary>
/// Abstraction over Azure Service Bus sending.
/// InMemoryServiceBusSender is used locally; AzureServiceBusSender in Azure.
/// </summary>
public interface IServiceBusSender
{
    Task SendAsync(ServiceBusJobMessage message, CancellationToken ct = default);

    /// <summary>Returns the most recent messages for display in the UI.</summary>
    IReadOnlyList<ServiceBusJobMessage> GetRecentMessages();
}
