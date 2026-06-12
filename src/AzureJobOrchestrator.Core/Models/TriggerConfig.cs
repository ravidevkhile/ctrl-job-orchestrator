using AzureJobOrchestrator.Core.Enums;
namespace AzureJobOrchestrator.Core.Models;
public class TriggerConfig
{
    public TriggerConfigType Type { get; set; } = TriggerConfigType.Manual;
    // Cron
    public string? CronExpression { get; set; }
    public string TimeZone { get; set; } = "UTC";
    // HTTP trigger
    public string? HttpToken { get; set; }  // simple bearer token for auth
    // Service Bus trigger
    public string? ServiceBusKeyVaultSecretName { get; set; }
    public string? ServiceBusQueueName { get; set; }
    public string? ServiceBusMessageFilter { get; set; }
}
