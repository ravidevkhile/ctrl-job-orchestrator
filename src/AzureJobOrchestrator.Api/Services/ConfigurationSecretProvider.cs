namespace AzureJobOrchestrator.Api.Services;
public sealed class ConfigurationSecretProvider(IConfiguration configuration) : ISecretProvider
{
    public Task<string?> GetSecretAsync(string secretName, CancellationToken ct = default)
    {
        // Looks up configuration key "Secrets:{secretName}"
        var value = configuration[$"Secrets:{secretName}"];
        return Task.FromResult(value);
    }
}
