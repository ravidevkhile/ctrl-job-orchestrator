using AzureJobOrchestrator.Api.Services;
using AzureJobOrchestrator.Core.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
namespace AzureJobOrchestrator.Api.Services.Templates;
public sealed class HttpCallExecutor(IHttpClientFactory httpClientFactory, ISecretProvider secretProvider, ILogger<HttpCallExecutor> logger) : ITemplateExecutor
{
    public string TemplateType => "HttpCall";
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);
    public async Task<TemplateResult> ExecuteAsync(JobDefinition job, JobRun run, CancellationToken ct = default)
    {
        var p = ParseParams(run.ParametersJson);
        var url = p.GetValueOrDefault("url")?.ToString();
        var method = p.GetValueOrDefault("method")?.ToString()?.ToUpper() ?? "GET";
        var body = p.GetValueOrDefault("body")?.ToString();
        var authType = p.GetValueOrDefault("authType")?.ToString() ?? "None";
        var authSecretName = p.GetValueOrDefault("authKeyVaultSecretName")?.ToString();
        var timeout = int.TryParse(p.GetValueOrDefault("timeoutSeconds")?.ToString(), out var t) ? t : 30;
        if (string.IsNullOrWhiteSpace(url))
            return new TemplateResult(false, "URL not configured.", ErrorMessage: "Missing 'url' parameter");
        try
        {
            var client = httpClientFactory.CreateClient("FunctionClient");
            client.Timeout = TimeSpan.FromSeconds(timeout);
            // Resolve auth token from Key Vault
            if (authType == "Bearer" && !string.IsNullOrWhiteSpace(authSecretName))
            {
                var token = await secretProvider.GetSecretAsync(authSecretName, ct);
                if (token is not null)
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            var request = new HttpRequestMessage(new HttpMethod(method), url);
            if (body is not null && method is "POST" or "PUT" or "PATCH")
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            var response = await client.SendAsync(request, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);
            var success = response.IsSuccessStatusCode;
            var log = $"[HttpCall] {method} {url}\n" +
                      $"Status: {(int)response.StatusCode} {response.ReasonPhrase}\n" +
                      $"Response: {responseBody[..Math.Min(500, responseBody.Length)]}";
            var output = JsonSerializer.Serialize(new { url, method, statusCode = (int)response.StatusCode, success, responseLength = responseBody.Length }, _json);
            logger.LogInformation("[HttpCall] {Method} {Url} -> {Status}", method, url, (int)response.StatusCode);
            return new TemplateResult(success, log, output, success ? null : $"HTTP {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[HttpCall] {Method} {Url} failed", method, url);
            return new TemplateResult(false, $"[HttpCall] Exception: {ex.Message}", ErrorMessage: ex.Message);
        }
    }
    private static Dictionary<string, object?> ParseParams(string json)
    {
        try { return JsonSerializer.Deserialize<Dictionary<string, object?>>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? []; }
        catch { return []; }
    }
}
