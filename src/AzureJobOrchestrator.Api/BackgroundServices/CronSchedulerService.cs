using Cronos;
using AzureJobOrchestrator.Api.Repositories;
using AzureJobOrchestrator.Api.Services;
using AzureJobOrchestrator.Core.Enums;

namespace AzureJobOrchestrator.Api.BackgroundServices;

public sealed class CronSchedulerService(
    IServiceScopeFactory scopeFactory,
    ILogger<CronSchedulerService> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("CronSchedulerService started.");
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(CheckInterval, stoppingToken);
            try { await CheckSchedulesAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Error in cron scheduler."); }
        }
    }

    private async Task CheckSchedulesAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var jobRepo = scope.ServiceProvider.GetRequiredService<IJobRepository>();
        var runner = scope.ServiceProvider.GetRequiredService<JobRunnerService>();

        var now = DateTimeOffset.UtcNow;

        foreach (var target in Enum.GetValues<JobTarget>())
        {
            var jobs = await jobRepo.ListByTargetAsync(target, ct);
            foreach (var job in jobs)
            {
                if (!job.Enabled) continue;
                if (job.TriggerConfig.Type != TriggerConfigType.Cron) continue;

                var expr = job.TriggerConfig.CronExpression ?? job.CronSchedule;
                if (string.IsNullOrWhiteSpace(expr)) continue;

                try
                {
                    var cronExpr = CronExpression.Parse(expr, CronFormat.Standard);
                    var tz = TimeZoneInfo.Utc;
                    try { tz = TimeZoneInfo.FindSystemTimeZoneById(job.TriggerConfig.TimeZone); } catch { }

                    var next = cronExpr.GetNextOccurrence(now.AddMinutes(-1), tz);
                    if (next is null || next > now.AddSeconds(30)) continue;

                    // Don't re-trigger if already ran within the last minute
                    if (job.LastRunAt.HasValue && (now - job.LastRunAt.Value).TotalMinutes < 1) continue;

                    logger.LogInformation("Cron trigger: job '{Name}' at {Time}", job.Name, now);
                    await runner.TriggerAsync(job.Id, TriggerType.Scheduled, "cron", ct: ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to evaluate cron for job {JobId}", job.Id);
                }
            }
        }
    }
}
