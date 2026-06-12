namespace AzureJobOrchestrator.Api.Services;
public interface ISecretProvider
{
    Task<string?> GetSecretAsync(string secretName, CancellationToken ct = default);
}
