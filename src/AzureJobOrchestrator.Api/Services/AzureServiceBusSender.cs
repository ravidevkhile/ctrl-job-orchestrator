using Azure.Messaging.ServiceBus;
using AzureJobOrchestrator.Core.Models;
using System.Collections.Concurrent;
using System.Text.Json;

namespace AzureJobOrchestrator.Api.Services;

/// <summary>
/// Sends messages to a real Azure Service Bus queue.
/// Used in Azure deployments; configure ServiceBus:ConnectionString and ServiceBus:QueueName.
/// </summary>
public sealed class AzureServiceBusSender(IConfiguration configuration, ILogger<AzureServiceBusSender> logger)
    : IServiceBusSender, IAsyncDisposable
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);
    private readonly ConcurrentQueue<ServiceBusJobMessage> _recent = new();
    private const int MaxRecent = 50;

    private ServiceBusSender? _sender;
    private ServiceBusClient? _client;

    private ServiceBusSender GetSender()
    {
        if (_sender is not null) return _sender;
        var connStr = configuration["ServiceBus:ConnectionString"]
            ?? throw new InvalidOperationException("ServiceBus:ConnectionString not configured.");
        var queue = configuration["ServiceBus:QueueName"] ?? "job-triggers";
        _client = new ServiceBusClient(connStr);
        _sender = _client.CreateSender(queue);
        return _sender;
    }

    public async Task SendAsync(ServiceBusJobMessage message, CancellationToken ct = default)
    {
        var body = JsonSerializer.Serialize(message, _json);
        var sbMessage = new ServiceBusMessage(body)
        {
            MessageId = message.MessageId,
            Subject = message.Action,
            ContentType = "application/json"
        };

        await GetSender().SendMessageAsync(sbMessage, ct);

        _recent.Enqueue(message);
        while (_recent.Count > MaxRecent) _recent.TryDequeue(out _);

        logger.LogInformation("Sent Service Bus message {MessageId} action={Action} target={Target}",
            message.MessageId, message.Action, message.TargetId);
    }

    public IReadOnlyList<ServiceBusJobMessage> GetRecentMessages() =>
        [.. _recent.Reverse()];

    public async ValueTask DisposeAsync()
    {
        if (_sender is not null) await _sender.DisposeAsync();
        if (_client is not null) await _client.DisposeAsync();
    }
}
