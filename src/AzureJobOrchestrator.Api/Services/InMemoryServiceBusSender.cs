using AzureJobOrchestrator.Core.Models;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace AzureJobOrchestrator.Api.Services;

/// <summary>
/// Local simulation of Azure Service Bus using an in-memory Channel.
///
/// Messages written here are read by ServiceBusListenerService (background
/// hosted service), which triggers the specified job or pipeline.
/// This lets the full Service Bus → job trigger flow be demoed locally
/// without any Azure infrastructure.
/// </summary>
public sealed class InMemoryServiceBusSender : IServiceBusSender
{
    // Unbounded channel so senders never block.
    public readonly Channel<ServiceBusJobMessage> Queue =
        Channel.CreateUnbounded<ServiceBusJobMessage>(new UnboundedChannelOptions { SingleReader = true });

    private readonly ConcurrentQueue<ServiceBusJobMessage> _recent = new();
    private const int MaxRecent = 50;

    public Task SendAsync(ServiceBusJobMessage message, CancellationToken ct = default)
    {
        // Keep a bounded history for the UI "recent messages" panel.
        _recent.Enqueue(message);
        while (_recent.Count > MaxRecent) _recent.TryDequeue(out _);

        Queue.Writer.TryWrite(message);
        return Task.CompletedTask;
    }

    public IReadOnlyList<ServiceBusJobMessage> GetRecentMessages() =>
        [.. _recent.Reverse()];  // newest-first
}
