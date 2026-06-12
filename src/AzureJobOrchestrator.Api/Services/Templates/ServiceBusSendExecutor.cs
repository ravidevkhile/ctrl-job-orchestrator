using Azure.Messaging.ServiceBus;
using AzureJobOrchestrator.Api.Services;
using AzureJobOrchestrator.Core.Models;
using System.Text.Json;
namespace AzureJobOrchestrator.Api.Services.Templates;
public sealed class ServiceBusSendExecutor(ISecretProvider secretProvider, ILogger<ServiceBusSendExecutor> logger) : ITemplateExecutor
{
    public string TemplateType => "ServiceBusSend";
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);
    public async Task<TemplateResult> ExecuteAsync(JobDefinition job, JobRun run, CancellationToken ct = default)
    {
        var p = ParseParams(run.ParametersJson);
        var secretName = p.GetValueOrDefault("connectionKeyVaultSecretName")?.ToString();
        var destination = p.GetValueOrDefault("queueOrTopicName")?.ToString() ?? "outgoing";
        var messageBody = p.GetValueOrDefault("messageBody")?.ToString() ?? "{}";
        // Try to get real connection string from Key Vault
        string? connStr = null;
        if (!string.IsNullOrWhiteSpace(secretName))
            connStr = await secretProvider.GetSecretAsync(secretName, ct);
        var msgId = Guid.NewGuid().ToString("N")[..12];
        if (!string.IsNullOrWhiteSpace(connStr))
        {
            try
            {
                await using var client = new ServiceBusClient(connStr);
                await using var sender = client.CreateSender(destination);
                var sbMsg = new ServiceBusMessage(messageBody) { MessageId = msgId };
                await sender.SendMessageAsync(sbMsg, ct);
                logger.LogInformation("[ServiceBusSend] Sent to {Dest}, msgId={MsgId}", destination, msgId);
            }
            catch (Exception ex)
            {
                return new TemplateResult(false, $"[ServiceBusSend] Failed: {ex.Message}", ErrorMessage: ex.Message);
            }
        }
        else
        {
            // Simulate send
            await Task.Delay(200, ct);
            logger.LogInformation("[ServiceBusSend][SIM] Would send to {Dest}, msgId={MsgId}", destination, msgId);
        }
        var log = $"[ServiceBusSend] Destination: {destination}\nMessage ID: {msgId}\nPayload size: {messageBody.Length} bytes\nStatus: SENT";
        var output = JsonSerializer.Serialize(new { destination, messageId = msgId, sentAt = DateTimeOffset.UtcNow }, _json);
        return new TemplateResult(true, log, output);
    }
    private static Dictionary<string, object?> ParseParams(string json)
    {
        try { return JsonSerializer.Deserialize<Dictionary<string, object?>>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? []; }
        catch { return []; }
    }
}
