using AzureJobOrchestrator.Api.Repositories;
using AzureJobOrchestrator.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AzureJobOrchestrator.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class RunsController(
    IRunRepository runRepo,
    JobRunnerService runner,
    ILogger<RunsController> logger) : ControllerBase
{
    // GET /api/runs/{runId}
    [HttpGet("{runId}")]
    public async Task<IActionResult> Get(string runId, CancellationToken ct)
    {
        var run = await runRepo.GetAsync(runId, ct);
        return run is null ? NotFound(new { error = "Run not found." }) : Ok(run);
    }

    // POST /api/runs/{runId}/rerun
    [HttpPost("{runId}/rerun")]
    public async Task<IActionResult> Rerun(string runId, CancellationToken ct)
    {
        try
        {
            var newRun = await runner.RerunAsync(runId, "ui", ct);
            return Ok(newRun);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (Exception ex)
        {
            logger.LogError(ex, "Rerun failed for run {RunId}", runId);
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
