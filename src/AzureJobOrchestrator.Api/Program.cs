using Azure.Data.Tables;
using Azure.Identity;
using AzureJobOrchestrator.Api.BackgroundServices;
using AzureJobOrchestrator.Api.Repositories;
using AzureJobOrchestrator.Api.Services;
using AzureJobOrchestrator.Api.Services.Templates;

var builder = WebApplication.CreateBuilder(args);

// ---- Configuration -------------------------------------------------------
// All sensitive values come from environment variables / Azure App Config.
// No secrets in code.

// ---- Logging + App Insights ----------------------------------------------
builder.Logging.AddConsole();
if (builder.Configuration["ApplicationInsights:ConnectionString"] is { Length: > 0 } aiConnStr)
{
    builder.Services.AddApplicationInsightsTelemetry(options =>
        options.ConnectionString = aiConnStr);
}

// ---- Azure Table Storage -------------------------------------------------
// Local: UseDevelopmentStorage=true (Azurite)
// Azure: use a managed-identity-aware endpoint or connection string from config
var storageConn = builder.Configuration["Storage:ConnectionString"]
    ?? "UseDevelopmentStorage=true";

builder.Services.AddSingleton(_ => new TableServiceClient(storageConn));

// ---- Repositories --------------------------------------------------------
builder.Services.AddSingleton<IJobRepository, TableStorageJobRepository>();
builder.Services.AddSingleton<IRunRepository, TableStorageRunRepository>();
builder.Services.AddSingleton<IPipelineRepository, TableStoragePipelineRepository>();
builder.Services.AddSingleton<IPipelineRunRepository, TableStoragePipelineRunRepository>();

// ---- Secret Provider -----------------------------------------------------
if (!string.IsNullOrWhiteSpace(builder.Configuration["KeyVault:Uri"]))
    builder.Services.AddSingleton<ISecretProvider, KeyVaultSecretProvider>();
else
    builder.Services.AddSingleton<ISecretProvider, ConfigurationSecretProvider>();

// ---- Template Executors --------------------------------------------------
builder.Services.AddSingleton<ITemplateExecutor, StoredProcedureExecutor>();
builder.Services.AddSingleton<ITemplateExecutor, HttpCallExecutor>();
builder.Services.AddSingleton<ITemplateExecutor, ServiceBusSendExecutor>();
builder.Services.AddSingleton<ITemplateExecutor, BlobOperationExecutor>();
builder.Services.AddSingleton<TemplateExecutorRegistry>();

// ---- Executors & Runners -------------------------------------------------
builder.Services.AddSingleton<BatchJobExecutor>();
builder.Services.AddSingleton<FunctionJobExecutor>();
builder.Services.AddSingleton<JobRunnerService>();
builder.Services.AddSingleton<PipelineRunnerService>();

// Named HTTP client for Azure Functions calls
builder.Services.AddHttpClient("FunctionClient", client =>
{
    client.Timeout = TimeSpan.FromSeconds(120);
});

// ---- Service Bus ---------------------------------------------------------
// Local dev: in-memory simulation.  Azure: set ServiceBus:ConnectionString to use real SB.
var useRealServiceBus = !string.IsNullOrWhiteSpace(builder.Configuration["ServiceBus:ConnectionString"]);
if (useRealServiceBus)
{
    builder.Services.AddSingleton<AzureServiceBusSender>();
    builder.Services.AddSingleton<IServiceBusSender>(sp => sp.GetRequiredService<AzureServiceBusSender>());
}
else
{
    // Register as singleton so ServiceBusListenerService can access the Channel directly.
    builder.Services.AddSingleton<InMemoryServiceBusSender>();
    builder.Services.AddSingleton<IServiceBusSender>(sp => sp.GetRequiredService<InMemoryServiceBusSender>());
    builder.Services.AddHostedService<ServiceBusListenerService>();
}

// ---- Background services -------------------------------------------------
builder.Services.AddHostedService<RunStatusPollerService>();
builder.Services.AddHostedService<CronSchedulerService>();

// ---- ASP.NET Core --------------------------------------------------------
builder.Services.AddControllers()
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // OpenApiInfo target type is inferred from the SwaggerDoc overload.
    options.SwaggerDoc("v1", new() { Title = "Azure Job Orchestrator API", Version = "v1" });
});

// CORS — allow the Vite dev server and any configured production origin
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173"];

builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(allowedOrigins)
          .AllowAnyHeader()
          .AllowAnyMethod()));

var app = builder.Build();

// ---- Middleware ----------------------------------------------------------
app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Job Orchestrator v1"));
}

app.UseHttpsRedirection();
app.MapControllers();

// ---- Seed sample data on first run ----------------------------------------
await SeedJobsAsync(app.Services);
await SeedPipelinesAsync(app.Services);

app.Run();

// ---- Seeder --------------------------------------------------------------
static async Task SeedJobsAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var jobRepo = scope.ServiceProvider.GetRequiredService<IJobRepository>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    // Only seed if there are no existing jobs.
    var existingBatch = await jobRepo.ListByTargetAsync(AzureJobOrchestrator.Core.Enums.JobTarget.Batch);
    var existingFn = await jobRepo.ListByTargetAsync(AzureJobOrchestrator.Core.Enums.JobTarget.Function);

    if (existingBatch.Count > 0 || existingFn.Count > 0)
    {
        logger.LogInformation("Jobs already seeded — skipping.");
        return;
    }

    logger.LogInformation("Seeding sample jobs…");

    var sampleBatchJobs = new[]
    {
        new AzureJobOrchestrator.Core.Models.JobDefinition
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Daily Data Validation",
            Target = AzureJobOrchestrator.Core.Enums.JobTarget.Batch,
            JobType = "DataValidation",
            ParametersJson = """{"dataset":"sales","threshold":0.95,"notifyOnFailure":true}""",
            CronSchedule = "0 6 * * *",
            Enabled = true,
            CreatedBy = "seed"
        },
        new AzureJobOrchestrator.Core.Models.JobDefinition
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Weekly Report Generation",
            Target = AzureJobOrchestrator.Core.Enums.JobTarget.Batch,
            JobType = "ReportGeneration",
            ParametersJson = """{"reportType":"weekly-summary","format":"pdf","recipients":["ops@example.com"]}""",
            CronSchedule = "0 8 * * MON",
            Enabled = true,
            CreatedBy = "seed"
        },
        new AzureJobOrchestrator.Core.Models.JobDefinition
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Document Processing Pipeline",
            Target = AzureJobOrchestrator.Core.Enums.JobTarget.Batch,
            JobType = "DocumentProcessing",
            ParametersJson = """{"sourceContainer":"incoming-docs","targetContainer":"processed-docs","ocr":true}""",
            Enabled = false,
            CreatedBy = "seed"
        }
    };

    var sampleFunctionJobs = new[]
    {
        new AzureJobOrchestrator.Core.Models.JobDefinition
        {
            Id = Guid.NewGuid().ToString(),
            Name = "API Health Check",
            Target = AzureJobOrchestrator.Core.Enums.JobTarget.Function,
            JobType = "DataValidation",
            ParametersJson = """{"endpoint":"https://api.example.com/health","timeout":5000}""",
            CronSchedule = "*/15 * * * *",
            Enabled = true,
            CreatedBy = "seed"
        },
        new AzureJobOrchestrator.Core.Models.JobDefinition
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Nightly Report Email",
            Target = AzureJobOrchestrator.Core.Enums.JobTarget.Function,
            JobType = "ReportGeneration",
            ParametersJson = """{"template":"nightly","to":["team@example.com"],"includeCharts":true}""",
            CronSchedule = "0 22 * * *",
            Enabled = true,
            CreatedBy = "seed"
        },
        new AzureJobOrchestrator.Core.Models.JobDefinition
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Invoice Ingestion",
            Target = AzureJobOrchestrator.Core.Enums.JobTarget.Function,
            JobType = "DocumentProcessing",
            ParametersJson = """{"sourceQueue":"invoices","extractLineItems":true,"currency":"USD"}""",
            Enabled = true,
            CreatedBy = "seed"
        }
    };

    foreach (var job in sampleBatchJobs.Concat(sampleFunctionJobs))
        await jobRepo.UpsertAsync(job);

    logger.LogInformation("Seeded {Count} sample jobs.", sampleBatchJobs.Length + sampleFunctionJobs.Length);
}

static async Task SeedPipelinesAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var pipelineRepo = scope.ServiceProvider.GetRequiredService<IPipelineRepository>();
    var jobRepo = scope.ServiceProvider.GetRequiredService<IJobRepository>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    var existing = await pipelineRepo.ListAsync();
    if (existing.Count > 0) return;

    // Get seeded job ids so pipeline steps can reference them.
    var batchJobs = await jobRepo.ListByTargetAsync(AzureJobOrchestrator.Core.Enums.JobTarget.Batch);
    var fnJobs = await jobRepo.ListByTargetAsync(AzureJobOrchestrator.Core.Enums.JobTarget.Function);

    if (batchJobs.Count < 3 || fnJobs.Count < 3) return;  // jobs not seeded yet

    logger.LogInformation("Seeding sample pipelines…");

    // Pipeline 1: Validate → Generate Report → Send Email (using Batch jobs)
    var batchPipeline = new AzureJobOrchestrator.Core.Models.PipelineDefinition
    {
        Id = Guid.NewGuid().ToString(),
        Name = "Batch: Validate → Report",
        Description = "Validates sales data then generates a report. Each step receives the previous step's output.",
        Enabled = true,
        CreatedBy = "seed",
        Steps =
        [
            new() { Order = 0, StepName = "Step 1 — Data Validation", JobDefinitionId = batchJobs[0].Id },
            new() { Order = 1, StepName = "Step 2 — Report Generation", JobDefinitionId = batchJobs[1].Id },
            new() { Order = 2, StepName = "Step 3 — Document Processing", JobDefinitionId = batchJobs[2].Id }
        ]
    };

    // Pipeline 2: Function-based ETL chain
    var fnPipeline = new AzureJobOrchestrator.Core.Models.PipelineDefinition
    {
        Id = Guid.NewGuid().ToString(),
        Name = "Functions: Ingest → Validate → Report",
        Description = "Ingests invoices, validates them, then emails a summary report.",
        Enabled = true,
        CreatedBy = "seed",
        Steps =
        [
            new() { Order = 0, StepName = "Step 1 — Invoice Ingestion", JobDefinitionId = fnJobs[2].Id },
            new() { Order = 1, StepName = "Step 2 — Health Check / Validate", JobDefinitionId = fnJobs[0].Id },
            new() { Order = 2, StepName = "Step 3 — Nightly Report", JobDefinitionId = fnJobs[1].Id }
        ]
    };

    await pipelineRepo.UpsertAsync(batchPipeline);
    await pipelineRepo.UpsertAsync(fnPipeline);
    logger.LogInformation("Seeded 2 sample pipelines.");
}
