using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
namespace AzureJobOrchestrator.Api.Services;
public sealed class KeyVaultSecretProvider(IConfiguration configuration, ILogger<KeyVaultSecretProvider> logger) : ISecretProvider
{
    private SecretClient? _client;
    private SecretClient GetClient()
    {
        if (_client is not null) return _client;
        var vaultUri = configuration["KeyVault:Uri"] ?? throw new InvalidOperationException("KeyVault:Uri not configured.");
        _client = new SecretClient(new Uri(vaultUri), new DefaultAzureCredential());
        return _client;
    }
    public async Task<string?> GetSecretAsync(string secretName, CancellationToken ct = default)
    {
        try
        {
            var response = await GetClient().GetSecretAsync(secretName, cancellationToken: ct);
            return response.Value.Value;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve secret {SecretName} from Key Vault", secretName);
            return null;
        }
    }
}
