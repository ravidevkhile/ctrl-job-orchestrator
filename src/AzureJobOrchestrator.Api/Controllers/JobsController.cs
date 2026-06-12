using AzureJobOrchestrator.Api.Repositories;
using AzureJobOrchestrator.Api.Services;
using AzureJobOrchestrator.Core.Enums;
using AzureJobOrchestrator.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace AzureJobOrchestrator.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class JobsController(
    IJobRepository jobRepo,
    IRunRepository runRepo,
    JobRunnerService runner,
    ILogger<JobsController> logger) : ControllerBase
{
    // GET /api/jobs?target=Batch|Function
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] JobTarget? target, CancellationToken ct)
    {
        if (target is null)
        {
            // Return all jobs when no filter is specified.
            var batch = await jobRepo.ListByTargetAsync(JobTarget.Batch, ct);
            var fn = await jobRepo.ListByTargetAsync(JobTarget.Function, ct);
            return Ok(batch.Concat(fn).OrderByDescending(j => j.CreatedAt));
        }
        return Ok(await jobRepo.ListByTargetAsync(target.Value, ct));
    }

    // GET /api/jobs/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct)
    {
        var job = await jobRepo.GetAsync(id, ct);
        return job is null ? NotFound(Error("Job not found.")) : Ok(job);
    }

    // POST /api/jobs
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateJobRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var job = new JobDefinition
        {
            Id = Guid.NewGuid().ToString(),
            Name = req.Name,
            Target = req.Target,
            JobType = req.JobType,
            ParametersJson = req.ParametersJson ?? "{}",
            CronSchedule = req.CronSchedule,
            Enabled = req.Enabled,
            CreatedBy = "ui",
            CreatedAt = DateTimeOffset.UtcNow
        };

        await jobRepo.UpsertAsync(job, ct);
        logger.LogInformation("Created job {JobId} ({JobName})", job.Id, job.Name);
        return CreatedAtAction(nameof(Get), new { id = job.Id }, job);
    }

    // PUT /api/jobs/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateJobRequest req, CancellationToken ct)
    {
        var job = await jobRepo.GetAsync(id, ct);
        if (job is null) return NotFound(Error("Job not found."));

        job.Name = req.Name ?? job.Name;
        job.JobType = req.JobType ?? job.JobType;
        job.ParametersJson = req.ParametersJson ?? job.ParametersJson;
        job.CronSchedule = req.CronSchedule ?? job.CronSchedule;
        job.Enabled = req.Enabled ?? job.Enabled;

        await jobRepo.UpsertAsync(job, ct);
        return Ok(job);
    }

    // PATCH /api/jobs/{id}/enabled
    [HttpPatch("{id}/enabled")]
    public async Task<IActionResult> SetEnabled(string id, [FromBody] SetEnabledRequest req, CancellationToken ct)
    {
        var job = await jobRepo.GetAsync(id, ct);
        if (job is null) return NotFound(Error("Job not found."));

        job.Enabled = req.Enabled;
        await jobRepo.UpsertAsync(job, ct);
        logger.LogInformation("Job {JobId} enabled={Enabled}", id, req.Enabled);
        return Ok(new { id, enabled = job.Enabled });
    }

    // DELETE /api/jobs/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        if (!await jobRepo.ExistsAsync(id, ct))
            return NotFound(Error("Job not found."));

        await jobRepo.DeleteAsync(id, ct);
        return NoContent();
    }

    // POST /api/jobs/{id}/run
    [HttpPost("{id}/run")]
    public async Task<IActionResult> RunNow(string id, [FromQuery] bool force = false, CancellationToken ct = default)
    {
        try
        {
            var run = await runner.TriggerAsync(id, TriggerType.Manual, "ui", force, ct);
            return Ok(run);
        }
        catch (KeyNotFoundException ex) { return NotFound(Error(ex.Message)); }
        catch (InvalidOperationException ex) { return Conflict(Error(ex.Message)); }
    }

    // GET /api/jobs/{id}/runs
    [HttpGet("{id}/runs")]
    public async Task<IActionResult> GetRuns(string id, [FromQuery] int max = 50, CancellationToken ct = default)
    {
        if (!await jobRepo.ExistsAsync(id, ct)) return NotFound(Error("Job not found."));
        return Ok(await runRepo.ListByJobAsync(id, max, ct));
    }

    private static object Error(string message) => new { error = message };
}

// ---- Request DTOs --------------------------------------------------------

public sealed record CreateJobRequest(
    [property: System.ComponentModel.DataAnnotations.Required] string Name,
    JobTarget Target,
    [property: System.ComponentModel.DataAnnotations.Required] string JobType,
    string? ParametersJson,
    string? CronSchedule,
    bool Enabled = true);

public sealed record UpdateJobRequest(
    string? Name,
    string? JobType,
    string? ParametersJson,
    string? CronSchedule,
    bool? Enabled);

public sealed record SetEnabledRequest(bool Enabled);
