using AzureJobOrchestrator.Api.Services;
using AzureJobOrchestrator.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace AzureJobOrchestrator.Api.Controllers;

/// <summary>
/// Exposes Service Bus send + recent-message-history to the UI.
///
/// Locally the messages go to the in-memory simulation queue;
/// in Azure they are sent to a real Service Bus queue and picked up by
/// the ServiceBusTriggerFunction in the Functions project.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class ServiceBusController(
    IServiceBusSender sender,
    ILogger<ServiceBusController> logger) : ControllerBase
{
    // POST /api/servicebus/messages
    [HttpPost("messages")]
    public async Task<IActionResult> Send([FromBody] SendMessageRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (req.Action is not ("TriggerJob" or "TriggerPipeline"))
            return BadRequest(new { error = "Action must be 'TriggerJob' or 'TriggerPipeline'." });

        var message = new ServiceBusJobMessage
        {
            MessageId = Guid.NewGuid().ToString(),
            Action = req.Action,
            TargetId = req.TargetId,
            PayloadJson = req.PayloadJson,
            SentBy = "ui",
            Notes = req.Notes,
            EnqueuedAt = DateTimeOffset.UtcNow
        };

        await sender.SendAsync(message, ct);
        logger.LogInformation("Service Bus message sent: {Action} → {Target}", req.Action, req.TargetId);
        return Ok(message);
    }

    // GET /api/servicebus/messages  — recent messages for the UI panel
    [HttpGet("messages")]
    public IActionResult GetRecent() => Ok(sender.GetRecentMessages());
}

public record SendMessageRequest(
    [property: System.ComponentModel.DataAnnotations.Required] string Action,
    [property: System.ComponentModel.DataAnnotations.Required] string TargetId,
    string? PayloadJson,
    string? Notes);
