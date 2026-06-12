/// <summary>
/// Batch Worker — runs as a task on an Azure Batch compute node.
///
/// Usage: worker.exe --jobType DataValidation --runId <guid> --params '{...}'
///
/// The worker reads the job type, executes the corresponding logic,
/// writes output to stdout (Batch captures this as stdout.txt), and exits
/// with 0 on success or non-zero on failure.
///
/// The BatchJobExecutor in the API project downloads stdout.txt and attaches
/// it to the run record as the job log.
/// </summary>

using Microsoft.Extensions.Logging;
using System.Text.Json;

var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
var logger = loggerFactory.CreateLogger("BatchWorker");

// Parse arguments
var jobType = GetArg(args, "--jobType") ?? "DataValidation";
var runId = GetArg(args, "--runId") ?? Guid.NewGuid().ToString();
var paramsJson = GetArg(args, "--params") ?? "{}";

logger.LogInformation("=== Azure Batch Worker ===");
logger.LogInformation("JobType : {JobType}", jobType);
logger.LogInformation("RunId   : {RunId}", runId);
logger.LogInformation("Params  : {Params}", paramsJson);

Dictionary<string, object?> parameters;
try
{
    parameters = JsonSerializer.Deserialize<Dictionary<string, object?>>(paramsJson,
        new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];
}
catch (JsonException ex)
{
    logger.LogError(ex, "Invalid parameters JSON.");
    return 1;
}

int exitCode;
try
{
    exitCode = jobType switch
    {
        "DataValidation"    => await RunDataValidationAsync(parameters, runId, logger),
        "ReportGeneration"  => await RunReportGenerationAsync(parameters, runId, logger),
        "DocumentProcessing"=> await RunDocumentProcessingAsync(parameters, runId, logger),
        _ => throw new InvalidOperationException($"Unknown job type: {jobType}")
    };
}
catch (Exception ex)
{
    logger.LogError(ex, "Job failed with unhandled exception.");
    exitCode = 1;
}

logger.LogInformation("Worker exiting with code {ExitCode}.", exitCode);
return exitCode;

// ---- Job implementations -------------------------------------------------

static async Task<int> RunDataValidationAsync(
    Dictionary<string, object?> p, string runId, ILogger logger)
{
    logger.LogInformation("[DataValidation] Starting validation…");
    var dataset = p.GetValueOrDefault("dataset")?.ToString() ?? "default";
    var threshold = double.TryParse(p.GetValueOrDefault("threshold")?.ToString(), out var t) ? t : 0.95;

    await Task.Delay(2_000);    // simulate I/O-bound work

    var recordsProcessed = Random.Shared.Next(10_000, 500_000);
    var score = Math.Round(0.92 + Random.Shared.NextDouble() * 0.08, 4);
    var passed = score >= threshold;

    logger.LogInformation("[DataValidation] Dataset={Dataset} Records={Records} Score={Score:P2} Threshold={Threshold:P2} Passed={Passed}",
        dataset, recordsProcessed, score, threshold, passed);

    if (!passed)
    {
        logger.LogError("[DataValidation] Validation FAILED. Score {Score} < threshold {Threshold}.", score, threshold);
        return 1;
    }

    logger.LogInformation("[DataValidation] Validation PASSED.");
    return 0;
}

static async Task<int> RunReportGenerationAsync(
    Dictionary<string, object?> p, string runId, ILogger logger)
{
    logger.LogInformation("[ReportGeneration] Generating report…");
    var reportType = p.GetValueOrDefault("reportType")?.ToString() ?? "summary";
    var format = p.GetValueOrDefault("format")?.ToString() ?? "pdf";

    await Task.Delay(3_000);

    var pages = Random.Shared.Next(10, 80);
    logger.LogInformation("[ReportGeneration] Type={Type} Format={Format} Pages={Pages}", reportType, format, pages);
    logger.LogInformation("[ReportGeneration] Report written to output/{RunId}/report.{Format}", runId, format);
    return 0;
}

static async Task<int> RunDocumentProcessingAsync(
    Dictionary<string, object?> p, string runId, ILogger logger)
{
    logger.LogInformation("[DocumentProcessing] Processing documents…");
    var source = p.GetValueOrDefault("sourceContainer")?.ToString() ?? "inbox";
    var ocr = p.GetValueOrDefault("ocr")?.ToString() == "true";

    await Task.Delay(4_000);

    var docs = Random.Shared.Next(5, 200);
    logger.LogInformation("[DocumentProcessing] Source={Source} OCR={Ocr} Documents={Docs}", source, ocr, docs);
    logger.LogInformation("[DocumentProcessing] Processing complete.");
    return 0;
}

// ---- Helpers -------------------------------------------------------------

static string? GetArg(string[] args, string name)
{
    var idx = Array.IndexOf(args, name);
    return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
}
