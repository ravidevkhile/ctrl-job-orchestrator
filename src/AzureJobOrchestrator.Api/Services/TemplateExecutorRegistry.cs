using AzureJobOrchestrator.Api.Services.Templates;
namespace AzureJobOrchestrator.Api.Services;
public sealed class TemplateExecutorRegistry(IEnumerable<ITemplateExecutor> executors)
{
    private readonly Dictionary<string, ITemplateExecutor> _map =
        executors.ToDictionary(e => e.TemplateType, StringComparer.OrdinalIgnoreCase);
    public ITemplateExecutor? Get(string templateType) =>
        _map.GetValueOrDefault(templateType);
    public IReadOnlyList<string> SupportedTypes => [.. _map.Keys];
}
