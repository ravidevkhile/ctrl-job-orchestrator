using AzureJobOrchestrator.Api.Repositories;
using AzureJobOrchestrator.Api.Services;
using AzureJobOrchestrator.Core.Enums;
using AzureJobOrchestrator.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace AzureJobOrchestrator.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class PipelinesController(
    IPipelineRepository pipelineRepo,
    IPipelineRunRepository pipelineRunRepo,
    PipelineRunnerService pipelineRunner,
    ILogger<PipelinesController> logger) : ControllerBase
{
    // GET /api/pipelines
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await pipelineRepo.ListAsync(ct));

    // GET /api/pipelines/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct)
    {
        var p = await pipelineRepo.GetAsync(id, ct);
        return p is null ? NotFound(Error("Pipeline not found.")) : Ok(p);
    }

    // POST /api/pipelines
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePipelineRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var pipeline = new PipelineDefinition
        {
            Id = Guid.NewGuid().ToString(),
            Name = req.Name,
            Description = req.Description ?? string.Empty,
            Enabled = req.Enabled,
            Steps = req.Steps.Select((s, i) => new PipelineStep
            {
                Order = i,
                StepName = s.StepName,
                JobDefinitionId = s.JobDefinitionId,
                OutputMappingJson = s.OutputMappingJson
            }).ToList(),
            CreatedBy = "ui",
            CreatedAt = DateTimeOffset.UtcNow
        };

        await pipelineRepo.UpsertAsync(pipeline, ct);
        logger.LogInformation("Created pipeline {Id} '{Name}'", pipeline.Id, pipeline.Name);
        return CreatedAtAction(nameof(Get), new { id = pipeline.Id }, pipeline);
    }

    // PUT /api/pipelines/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdatePipelineRequest req, CancellationToken ct)
    {
        var pipeline = await pipelineRepo.GetAsync(id, ct);
        if (pipeline is null) return NotFound(Error("Pipeline not found."));

        pipeline.Name = req.Name ?? pipeline.Name;
        pipeline.Description = req.Description ?? pipeline.Description;
        pipeline.Enabled = req.Enabled ?? pipeline.Enabled;
        if (req.Steps is not null)
            pipeline.Steps = req.Steps.Select((s, i) => new PipelineStep
            {
                Order = i,
                StepName = s.StepName,
                JobDefinitionId = s.JobDefinitionId,
                OutputMappingJson = s.OutputMappingJson
            }).ToList();

        await pipelineRepo.UpsertAsync(pipeline, ct);
        return Ok(pipeline);
    }

    // PATCH /api/pipelines/{id}/enabled
    [HttpPatch("{id}/enabled")]
    public async Task<IActionResult> SetEnabled(string id, [FromBody] SetEnabledRequest req, CancellationToken ct)
    {
        var pipeline = await pipelineRepo.GetAsync(id, ct);
        if (pipeline is null) return NotFound(Error("Pipeline not found."));
        pipeline.Enabled = req.Enabled;
        await pipelineRepo.UpsertAsync(pipeline, ct);
        return Ok(new { id, enabled = pipeline.Enabled });
    }

    // DELETE /api/pipelines/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        if (!await pipelineRepo.ExistsAsync(id, ct))
            return NotFound(Error("Pipeline not found."));
        await pipelineRepo.DeleteAsync(id, ct);
        return NoContent();
    }

    // POST /api/pipelines/{id}/run
    [HttpPost("{id}/run")]
    public async Task<IActionResult> Run(string id, CancellationToken ct)
    {
        try
        {
            var run = await pipelineRunner.TriggerAsync(id, TriggerType.Manual, "ui", ct: ct);
            return Ok(run);
        }
        catch (KeyNotFoundException ex) { return NotFound(Error(ex.Message)); }
        catch (InvalidOperationException ex) { return Conflict(Error(ex.Message)); }
    }

    // GET /api/pipelines/{id}/runs
    [HttpGet("{id}/runs")]
    public async Task<IActionResult> GetRuns(string id, [FromQuery] int max = 20, CancellationToken ct = default)
    {
        if (!await pipelineRepo.ExistsAsync(id, ct)) return NotFound(Error("Pipeline not found."));
        return Ok(await pipelineRunRepo.ListByPipelineAsync(id, max, ct));
    }

    // GET /api/pipelines/runs/{runId}
    [HttpGet("runs/{runId}")]
    public async Task<IActionResult> GetRun(string runId, CancellationToken ct)
    {
        var run = await pipelineRunRepo.GetAsync(runId, ct);
        return run is null ? NotFound(Error("Run not found.")) : Ok(run);
    }

    private static object Error(string msg) => new { error = msg };
}

// ---- DTOs ----------------------------------------------------------------

public record CreatePipelineRequest(
    [property: System.ComponentModel.DataAnnotations.Required] string Name,
    string? Description,
    bool Enabled,
    List<PipelineStepRequest> Steps);

public record UpdatePipelineRequest(
    string? Name,
    string? Description,
    bool? Enabled,
    List<PipelineStepRequest>? Steps);

public record PipelineStepRequest(
    string StepName,
    string JobDefinitionId,
    string? OutputMappingJson);
