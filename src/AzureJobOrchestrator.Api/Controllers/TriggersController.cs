using AzureJobOrchestrator.Api.Repositories;
using AzureJobOrchestrator.Api.Services;
using AzureJobOrchestrator.Core.Enums;
using Microsoft.AspNetCore.Mvc;

namespace AzureJobOrchestrator.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class TriggersController(
    IJobRepository jobRepo,
    JobRunnerService runner) : ControllerBase
{
    // POST /api/triggers/{jobId}/http
    // Simple token auth via X-Trigger-Token header or ?token= query param
    [HttpPost("{jobId}/http")]
    public async Task<IActionResult> HttpTrigger(string jobId, [FromHeader(Name = "X-Trigger-Token")] string? headerToken, [FromQuery] string? token, CancellationToken ct)
    {
        var job = await jobRepo.GetAsync(jobId, ct);
        if (job is null) return NotFound(new { error = "Job not found." });
        if (job.TriggerConfig.Type != TriggerConfigType.Http && job.TriggerConfig.HttpToken is not null)
            return BadRequest(new { error = "Job is not configured for HTTP trigger." });

        // Validate token if configured
        var configuredToken = job.TriggerConfig.HttpToken;
        var providedToken = headerToken ?? token;
        if (!string.IsNullOrWhiteSpace(configuredToken) && configuredToken != providedToken)
            return Unauthorized(new { error = "Invalid trigger token." });

        var run = await runner.TriggerAsync(jobId, TriggerType.Manual, "http-trigger", ct: ct);
        return Ok(new { message = "Job triggered", runId = run.Id, status = run.Status.ToString() });
    }

    // GET /api/triggers/{jobId}/http — returns the trigger URL for sharing
    [HttpGet("{jobId}/http")]
    public async Task<IActionResult> GetTriggerInfo(string jobId, CancellationToken ct)
    {
        var job = await jobRepo.GetAsync(jobId, ct);
        if (job is null) return NotFound(new { error = "Job not found." });
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        return Ok(new
        {
            triggerUrl = $"{baseUrl}/api/triggers/{jobId}/http",
            method = "POST",
            headers = new { XTriggerToken = job.TriggerConfig.HttpToken is not null ? "<your-token>" : "not required" },
            jobName = job.Name,
            triggerType = job.TriggerConfig.Type.ToString()
        });
    }
}
