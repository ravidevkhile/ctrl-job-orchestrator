# Azure Job Orchestrator — Full Documentation

> **Stack:** .NET 10 · ASP.NET Core · Azure Functions (isolated worker) · Azure Batch · Azure Table Storage · Azure Service Bus · Azure Key Vault · React 18 · MUI v6 · TypeScript · Vite

---

## Table of Contents

1. [Platform Overview](#1-platform-overview)
2. [Feature Reference](#2-feature-reference)
3. [Azure Batch vs Azure Functions — Decision Guide](#3-azure-batch-vs-azure-functions--decision-guide)
4. [Azure Resource Requirements](#4-azure-resource-requirements)
5. [Job Template Catalogue](#5-job-template-catalogue)
6. [Trigger System](#6-trigger-system)
7. [Pipeline Orchestration](#7-pipeline-orchestration)
8. [Service Bus Integration](#8-service-bus-integration)
9. [Key Vault & Secrets](#9-key-vault--secrets)
10. [Local Development Setup](#10-local-development-setup)
11. [Working Session Guide — Real-World Examples](#11-working-session-guide--real-world-examples)
12. [Architecture Reference](#12-architecture-reference)
13. [API Reference](#13-api-reference)

---

## 1. Platform Overview

Azure Job Orchestrator is a full-stack job scheduling and pipeline platform that lets you define, trigger, monitor, and chain jobs across two Azure compute surfaces — **Azure Batch** (for heavy workloads) and **Azure Functions** (for lightweight, event-driven tasks) — from a single React web UI.

### Core Capabilities

| Capability | Description |
|---|---|
| Job definition | Create typed job definitions with templates, parameters, and trigger configurations |
| Four trigger types | Manual, Cron schedule, HTTP webhook, Service Bus message |
| Pipeline chaining | Multi-step pipelines where each step's output is injected into the next step's parameters |
| Service Bus integration | Publish messages to trigger jobs or pipelines asynchronously |
| Key Vault secrets | Connection strings and auth tokens resolved from Azure Key Vault at runtime |
| 7 job templates | DataValidation, ReportGeneration, DocumentProcessing, StoredProcedure, HttpCall, ServiceBusSend, BlobOperation |
| Simulation mode | Full local development without any Azure infrastructure |
| Live status polling | UI polls every 5 seconds; status badges animate during active runs |
| Run history & logs | Per-run log output, duration, error messages, and structured output JSON |
| Rerun | Any historical run can be rerun with its original parameters |

---

## 2. Feature Reference

### 2.1 Dashboard

The Dashboard tab provides a real-time overview of the entire platform.

- **Stat cards** — Total Jobs, Active Runs, Succeeded Today, Failed Today
- **Bar chart** — Job runs per day over the last 7 days, broken down by Batch vs Functions
- **Donut chart** — Last-run status distribution across all jobs
- **Recent activity table** — Jobs sorted by most recent run, showing status, target, trigger type, and timestamp
- Auto-refreshes every 10 seconds

### 2.2 Job Management (Batch & Functions tabs)

Each target (Azure Batch, Azure Functions) has its own tab with a full job table.

**Table columns:**

| Column | Description |
|---|---|
| Name | Job name + cron expression (shown below name for Cron triggers) |
| Template | The template type driving this job's behaviour |
| Trigger | Trigger type chip: Manual / Cron / HTTP / ServiceBus |
| Enabled | Toggle switch — enables or disables job execution |
| Last Status | Animated status badge |
| Last Run | Timestamp of the most recent execution |
| Actions | Run Now button + context menu (View History, Edit, Delete) |

**Job Wizard — 4-step creation dialog:**

1. **Choose Target** — Azure Batch or Azure Functions, with description cards
2. **Select Template** — 7 template cards showing tier (Standard/Enterprise), icon, and description
3. **Configure Trigger** — 4-tab trigger configuration (Manual / Cron / HTTP / ServiceBus)
4. **Parameters & Review** — Template-specific parameter form + summary chip strip

### 2.3 Pipeline Designer

Pipelines chain multiple jobs so that each step's output is forwarded as `previousStepOutput` in the next step's parameters.

**Pipeline table columns:** Name, Steps (chip list), Enabled toggle, Last Status, Last Run, Actions (Run, Edit, History, Delete)

**Pipeline Form Dialog:**
- Name and description fields
- Drag-and-drop step canvas (powered by `@dnd-kit`) — drag the handle to reorder steps
- Each step: step name field + job selector dropdown (shows Batch/Function target chip)
- Arrow connectors visually connect steps in sequence
- Add step button (appends or inserts after current step)

**Pipeline Run Drawer:**
- Per-step status cards: icon (Pending / Running spinner / Succeeded / Failed / Skipped)
- Step duration chip once completed
- Structured output JSON panel (collapsed pre-formatted block) showing data passed to the next step
- Progress bar: `currentStep / totalSteps`
- Auto-polls every 3 seconds while the pipeline is running

### 2.4 Service Bus Tab

Dedicated panel for sending messages to the in-memory (or real Azure) Service Bus queue.

- **Action selector** — TriggerJob or TriggerPipeline
- **Target selector** — dropdown populated with all jobs or pipelines
- **Payload JSON** — optional extra parameters merged into the target's parameter set
- **Notes** — free-text annotation stored with the message
- **Recent messages feed** — live list of sent messages with action chip, target name, notes, timestamp, and message ID (auto-refreshes every 3 seconds)

### 2.5 Templates Gallery

Browsable card gallery showing all 7 job templates with:
- Icon and description
- Standard vs Enterprise tier tag
- Click to select

### 2.6 Run History Drawer

Right-hand drawer opened from any job's context menu:
- List of all runs, newest first
- Per-run: status badge, trigger type, user, start time, duration
- Log output (dark terminal-style block, max height 120px with scroll)
- Error message (in red) if the run failed
- Output location link if present
- **Rerun** button on each historical run

---

## 3. Azure Batch vs Azure Functions — Decision Guide

Choosing the right compute surface is the most important architectural decision per job type.

### 3.1 Azure Batch — When to Use

Azure Batch is an HPC-class managed compute service that provisions a pool of VMs, distributes work across nodes, and handles scaling, task retry, and result collection automatically.

**Use Azure Batch when:**

| Scenario | Reason |
|---|---|
| Processing millions of records or files | Can fan out across hundreds of nodes in parallel |
| Jobs run for minutes to hours | Batch has no execution time limit per task |
| CPU or GPU intensive workloads | Pool nodes can be any VM SKU including GPU VMs |
| Machine learning training or batch inference | Data stays on the pool network; supports MPI |
| ETL pipelines over large datasets | Output written directly to Blob Storage or Data Lake |
| Encoding, transcoding, rendering | Media processing benefits from parallel node execution |
| You need task-level retry and output tracking | Batch has built-in task retry policies and stdout/stderr capture |
| Cost must be minimised at scale | Low-priority (spot) nodes reduce costs by up to 80% |

**Azure Batch is NOT ideal when:**

- Jobs run for less than ~30 seconds (pool warm-up overhead amortises poorly)
- You need sub-second invocation latency
- You want HTTP-triggered, on-demand, event-driven execution
- You want consumption-based billing with zero idle cost

### 3.2 Azure Functions — When to Use

Azure Functions is a serverless, event-driven compute service. Functions start on-demand, bill only for execution time, and scale to zero when idle.

**Use Azure Functions when:**

| Scenario | Reason |
|---|---|
| HTTP webhooks and REST callbacks | Native HTTP trigger, response in milliseconds |
| Reacting to blob uploads or queue messages | Built-in blob, queue, and Service Bus triggers |
| Short-lived orchestrations (< 10 min) | Consumption plan timeout is 10 min; Flex is longer |
| Sending notifications or emails | Light, stateless, cost-effective |
| API integrations and data enrichment | Low-latency HTTP calls to third-party services |
| Scheduled micro-tasks | Timer trigger with cron expression |
| Event-driven database writes | Cosmos DB, SQL, or Table Storage output bindings |
| Lightweight document parsing | Fast, stateless, scales to zero |

**Azure Functions is NOT ideal when:**

- Jobs require > 10 minutes on Consumption plan (use Batch, ACI, or App Service)
- You need GPU access or custom VM sizes
- You need to maintain local state across multiple tasks
- You process terabytes of data (network and memory limits apply)

### 3.3 Side-by-Side Comparison

| Dimension | Azure Batch | Azure Functions |
|---|---|---|
| **Billing model** | VM cost per hour (spot available) | Per-execution + per-GB-second |
| **Max execution time** | Unlimited | 10 min (Consumption) / unlimited (Dedicated) |
| **Cold start** | Pool warm-up: minutes | Sub-second (Consumption) |
| **Scaling unit** | Pool VM nodes | Function instances |
| **Max parallelism** | Pool node count × tasks per node | Thousands of concurrent instances |
| **Custom runtimes** | Any binary on the VM | .NET, Node, Python, Java, PowerShell |
| **GPU support** | Yes (NC/ND VM series) | No |
| **MPI / inter-node comms** | Yes | No |
| **Output destinations** | Blob, stdout/stderr, custom | Any binding output |
| **Managed identity** | Via pool identity | Via function app identity |
| **Deployment unit** | Batch application packages / Docker | ZIP deploy, Docker, source deploy |
| **Best fit** | Heavy compute, long-running, data-at-scale | Event-driven, short, lightweight |

---

## 4. Azure Resource Requirements

### 4.1 Always Required (Both Targets)

| Resource | Purpose | SKU / Tier |
|---|---|---|
| **Azure Table Storage** | Job definitions, run history, pipeline definitions, pipeline run history | Standard LRS (Azurite locally) |
| **Azure Storage Account** | Backs Table Storage; also used for Batch task files and Function host storage | Standard LRS |
| **Resource Group** | Logical container for all resources | N/A |
| **App Service Plan** (API) | Hosts the ASP.NET Core API | B1 minimum; P1v3 recommended for prod |
| **App Service** (API) | Hosts `AzureJobOrchestrator.Api` | Linux, .NET 10 |

### 4.2 Azure Batch Requirements

| Resource | Purpose | Notes |
|---|---|---|
| **Batch Account** | Manages pools, jobs, and tasks | One per region |
| **Pool** | VM nodes that run tasks | OS: Ubuntu 20.04 or Windows Server; size depends on workload |
| **Storage Account** (linked to Batch) | Stores task resource files (the BatchWorker binary) | Same account as Table Storage is fine |
| **Managed Identity** (pool identity) | Allows pool VMs to read Blob Storage and Key Vault without credentials | Assigned to the pool, not the task |
| **Virtual Network** (optional) | If pool nodes need to reach private SQL or on-prem resources | Standard VNet + subnet dedicated to Batch |

**Batch account configuration in `appsettings.json`:**
```json
"Batch": {
  "AccountName": "<batch-account-name>",
  "AccountKey":  "<key-or-use-managed-identity>",
  "AccountUrl":  "https://<account>.<region>.batch.azure.com",
  "PoolId":      "job-orchestrator-pool",
  "SimulationMode": false
}
```

**Pool node requirements for the BatchWorker:**
- .NET 10 runtime installed (via start task or custom VM image)
- Access to the storage account where task resource files are stored
- Outbound HTTPS to Azure endpoints

### 4.3 Azure Functions Requirements

| Resource | Purpose | Notes |
|---|---|---|
| **Function App** | Hosts `AzureJobOrchestrator.Functions` | Linux (Flex Consumption) or Windows Consumption |
| **App Service Plan** | Underlies the Function App | Flex Consumption: serverless; Dedicated: always-warm |
| **Storage Account** | Functions host runtime storage | Can share with API storage account |
| **Application Insights** (recommended) | Telemetry, invocation logs, performance | Link via `APPLICATIONINSIGHTS_CONNECTION_STRING` |

**Required app settings on the Function App:**
```
FUNCTIONS_WORKER_RUNTIME   = dotnet-isolated
AzureWebJobsStorage        = <connection-string>
APPLICATIONINSIGHTS_CONNECTION_STRING = <optional>
```

**Function endpoint configuration in API `appsettings.json`:**
```json
"Functions": {
  "Endpoints": {
    "DataValidation":    "https://<app>.azurewebsites.net/api/DataValidation?code=<key>",
    "ReportGeneration":  "https://<app>.azurewebsites.net/api/ReportGeneration?code=<key>",
    "DocumentProcessing":"https://<app>.azurewebsites.net/api/DocumentProcessing?code=<key>"
  }
}
```

### 4.4 Azure Service Bus (Optional — for ServiceBus triggers)

| Resource | Purpose | Notes |
|---|---|---|
| **Service Bus Namespace** | Hosts queues and topics | Standard tier minimum (Basic has no topics) |
| **Queue: `job-triggers`** | Receives `ServiceBusJobMessage` payloads | 1 MB max message size is sufficient |
| **Managed Identity** (API) | API sends messages; Functions listener reads messages | Role: `Azure Service Bus Data Sender` + `Data Receiver` |

**Configuration in `appsettings.json`:**
```json
"ServiceBus": {
  "ConnectionString": "<primary-connection-string-or-managed-identity-endpoint>"
}
```

When `ServiceBus:ConnectionString` is empty, the platform uses the **in-memory channel simulation** — no Service Bus account is needed locally.

### 4.5 Azure Key Vault (Optional — for secrets in templates)

| Resource | Purpose | Notes |
|---|---|---|
| **Key Vault** | Stores database connection strings, API tokens, SB connection strings | Standard SKU |
| **Managed Identity** (API App Service) | Allows API to read secrets without credentials in config | Role: `Key Vault Secrets User` |

**Configuration in `appsettings.json`:**
```json
"KeyVault": {
  "Uri": "https://<vault-name>.vault.azure.net/"
}
```

When `KeyVault:Uri` is empty, the `ConfigurationSecretProvider` reads secrets from the `Secrets:` section of `appsettings.json` — safe for local development.

**Local dev secrets config:**
```json
"Secrets": {
  "MyDbConnectionString": "Server=localhost;Database=MyDb;...",
  "MyApiToken":           "Bearer eyJ..."
}
```

### 4.6 Database (for StoredProcedure template)

| Option | Notes |
|---|---|
| **Azure SQL Database** | Recommended; connection string stored in Key Vault |
| **Azure SQL Managed Instance** | For existing MI workloads |
| **SQL Server on-prem** | Reachable via Private Link or VPN from Batch pool / Function App |
| **PostgreSQL** | Supported by changing the ADO.NET provider in `StoredProcedureExecutor` |

The `StoredProcedureExecutor` uses the Key Vault secret name from the job's parameters to resolve the connection string at runtime. No connection string ever appears in source code or config files.

### 4.7 Blob Storage (for BlobOperation template)

| Resource | Purpose |
|---|---|
| **Storage Account containers** | Source and destination containers for archive or delete operations |
| **Managed Identity** (API or Batch pool) | `Storage Blob Data Contributor` role on the storage account |

---

## 5. Job Template Catalogue

### DataValidation
**Target:** Batch (heavy datasets) or Functions (API-based validation)

Validates a dataset against configurable quality thresholds. Returns pass/fail result, row counts, quality score, and failed field statistics.

**Parameters:**
| Parameter | Type | Description |
|---|---|---|
| `dataset` | string | Dataset name or path to validate |
| `threshold` | float (0–1) | Minimum quality score to pass (e.g. `0.95` = 95%) |
| `notifyOnFailure` | bool | Send notification if validation fails |

**Output JSON:**
```json
{ "dataset": "sales", "rowsChecked": 48200, "qualityScore": 0.982, "passed": true, "failedFields": [] }
```

---

### ReportGeneration
**Target:** Batch (large reports, PDF rendering) or Functions (quick HTML/CSV)

Generates and distributes scheduled reports in PDF, HTML, or CSV format.

**Parameters:**
| Parameter | Type | Description |
|---|---|---|
| `template` | string | Report template name: `weekly-summary`, `monthly-report`, `nightly`, `executive-dashboard` |
| `format` | string | Output format: `pdf`, `html`, `csv` |
| `recipients` | string | Comma-separated email addresses |

**Output JSON:**
```json
{ "template": "weekly-summary", "format": "pdf", "recipientCount": 3, "reportSizeKb": 842, "outputPath": "reports/2024-01/weekly-summary.pdf" }
```

---

### DocumentProcessing
**Target:** Batch (high-volume OCR) or Functions (single-document processing)

Processes documents between blob storage containers with optional OCR extraction.

**Parameters:**
| Parameter | Type | Description |
|---|---|---|
| `sourceContainer` | string | Input blob container name |
| `targetContainer` | string | Output blob container name |
| `ocr` | bool | Enable OCR text extraction |

**Output JSON:**
```json
{ "sourceContainer": "incoming-docs", "targetContainer": "processed-docs", "documentsProcessed": 127, "ocrEnabled": true, "pagesExtracted": 1840 }
```

---

### StoredProcedure
**Target:** Functions (fast, event-driven) or Batch (long-running SP with large result sets)

Executes a SQL stored procedure with Key Vault-managed connection string.

**Parameters:**
| Parameter | Type | Description |
|---|---|---|
| `keyVaultSecretName` | string | Name of the Key Vault secret containing the DB connection string |
| `storedProcedureName` | string | Fully qualified SP name (e.g. `dbo.usp_GenerateMonthlyReport`) |
| `inputParameters` | JSON string | Parameters passed to the SP as key-value pairs |
| `commandTimeoutSeconds` | int | Query timeout (default 30) |

**Output JSON:**
```json
{ "storedProcedure": "dbo.usp_GenerateMonthlyReport", "rowsAffected": 1247, "executionMs": 3210, "outputParams": { "reportId": "RPT-2024-01" } }
```

---

### HttpCall
**Target:** Functions (preferred — fast, lightweight HTTP client)

Makes an authenticated HTTP request to any external API endpoint.

**Parameters:**
| Parameter | Type | Description |
|---|---|---|
| `url` | string | Full URL including query string |
| `method` | string | HTTP method: `GET`, `POST`, `PUT`, `DELETE`, `PATCH` |
| `authType` | string | `None`, `Bearer`, `ApiKey` |
| `authKeyVaultSecretName` | string | Key Vault secret name for the auth token (if `authType` ≠ `None`) |
| `body` | JSON string | Request body for non-GET requests |
| `retryCount` | int | Number of retries on transient failure (0–5) |
| `timeoutSeconds` | int | Per-request timeout |

**Output JSON:**
```json
{ "url": "https://api.example.com/data", "method": "POST", "statusCode": 200, "responseTimeMs": 342, "responseBody": "..." }
```

---

### ServiceBusSend
**Target:** Functions (event-driven message publishing)

Sends a message to an Azure Service Bus queue or topic.

**Parameters:**
| Parameter | Type | Description |
|---|---|---|
| `connectionKeyVaultSecretName` | string | Key Vault secret for the Service Bus connection string |
| `queueOrTopicName` | string | Queue or topic name |
| `messageBody` | JSON string | Message payload |
| `messageProperties` | JSON string | Additional message properties (key-value) |

**Output JSON:**
```json
{ "queue": "outgoing-messages", "messageId": "msg-abc123", "enqueuedAt": "2024-01-15T09:00:00Z" }
```

---

### BlobOperation
**Target:** Batch (bulk archival over thousands of files) or Functions (triggered on schedule)

Archives or deletes blob files based on age and pattern filters.

**Parameters:**
| Parameter | Type | Description |
|---|---|---|
| `containerName` | string | Blob container to operate on |
| `folderPath` | string | Path prefix filter (e.g. `/logs/2023`) |
| `fileExtension` | string | File extension filter (`*` for all) |
| `operation` | string | `Archive` (move to cool/archive tier) or `Delete` |
| `retentionDays` | int | Process files older than this many days |

**Output JSON:**
```json
{ "container": "data", "operation": "Archive", "filesProcessed": 342, "sizeGbReleased": 18.4, "cutoffDate": "2023-10-15" }
```

---

## 6. Trigger System

Every job has a `TriggerConfig` that determines how it starts. Trigger type can be changed at any time by editing the job.

### Manual
The job only runs when explicitly triggered from the UI ("Run Now" button) or via `POST /api/jobs/{id}/run`.

No configuration required.

### Cron Schedule
The `CronSchedulerService` background service checks all enabled Cron jobs every 30 seconds and fires any job whose cron expression has elapsed since its last run.

**Configuration:**
- Expression: standard 5-field cron (`min hour day month weekday`)
- Timezone: IANA timezone name (default: `UTC`)

**Example expressions:**

| Expression | Meaning |
|---|---|
| `* * * * *` | Every minute |
| `*/5 * * * *` | Every 5 minutes |
| `0 6 * * *` | Daily at 06:00 UTC |
| `0 8 * * MON-FRI` | Weekdays at 08:00 |
| `0 0 1 * *` | First of every month |

### HTTP Webhook
Exposes a POST endpoint: `POST /api/triggers/{jobId}/http`

Optional token auth: set `httpToken` in the trigger config; the caller must include `X-Trigger-Token: <token>` in the request header.

The trigger URL is shown in the job wizard and can be copied directly.

### Service Bus
The `ServiceBusListenerService` reads messages from the `job-triggers` queue. A `ServiceBusJobMessage` with `Action = "TriggerJob"` and `TargetId = jobId` fires the job immediately. The message filter field allows SQL-style filtering of incoming messages.

---

## 7. Pipeline Orchestration

### How Pipelines Work

1. A pipeline run is created with `Status = Pending`
2. Step 1 is triggered as a normal job run
3. When Step 1 completes, its `StructuredOutputJson` is read
4. The output is merged into Step 2's parameters under the key `previousStepOutput`
5. Step 2 runs with the enriched parameters
6. This repeats until all steps complete or one fails
7. Pipeline run status reflects the worst step status

### Parameter Injection

When Step N completes, the runner calls `MergeParameters`:

```json
// Step 2 receives this parameter JSON:
{
  "dataset": "sales",
  "threshold": 0.95,
  "previousStepOutput": {
    "rowsChecked": 48200,
    "qualityScore": 0.982,
    "passed": true
  }
}
```

Each step's template executor can read `previousStepOutput` to conditionally branch or use upstream values.

### Pipeline Failure Behaviour

- If a step fails, the pipeline is marked `Failed` and subsequent steps are skipped (`Skipped` status)
- The pipeline run drawer shows which step failed and the error message
- Individual steps can be rerun as standalone job runs from their run history

---

## 8. Service Bus Integration

### Message Schema

```json
{
  "messageId":  "msg-uuid",
  "action":     "TriggerJob | TriggerPipeline",
  "targetId":   "<job-id or pipeline-id>",
  "payloadJson": "{}",
  "sentBy":     "user@example.com",
  "enqueuedAt": "2024-01-15T09:00:00Z",
  "notes":      "Optional human-readable annotation"
}
```

### Local Simulation

In local development, `InMemoryServiceBusSender` uses a `System.Threading.Channels.Channel<T>` in place of real Service Bus. Messages sent from the UI are written to the channel, and `ServiceBusListenerService` reads from it and fires jobs/pipelines. This means the full Service Bus flow works completely offline.

### Production

Set `ServiceBus:ConnectionString` in `appsettings.json` or as an environment variable. The `AzureServiceBusSender` sends to the real queue, and the `ServiceBusListenerService` reads from it. For managed identity auth, use the namespace endpoint instead of a connection string.

---

## 9. Key Vault & Secrets

The `ISecretProvider` abstraction decouples secret resolution from any specific store:

| Environment | Provider | How |
|---|---|---|
| Local dev | `ConfigurationSecretProvider` | Reads `Secrets:{name}` from `appsettings.json` |
| Azure | `KeyVaultSecretProvider` | Uses `DefaultAzureCredential` → Managed Identity |

Template executors that need a secret (database connection, API token, SB connection string) receive the **secret name** in their job parameters, then call `ISecretProvider.GetSecretAsync(name)` at runtime. The actual secret value never appears in any job definition or parameter form.

**Granting access (Azure):**

```bash
# Grant the API's managed identity read access to Key Vault secrets
az keyvault set-policy \
  --name <vault-name> \
  --object-id <api-managed-identity-object-id> \
  --secret-permissions get list
```

---

## 10. Local Development Setup

### Prerequisites

| Tool | Version | Check |
|---|---|---|
| .NET SDK | 10.0 | `dotnet --version` |
| Node.js | 20 LTS+ | `node --version` |
| Azure Functions Core Tools | v4 | `func --version` |
| Azurite | latest | `npx azurite --version` |

### Step-by-Step

**Terminal 1 — Azurite (Table Storage emulator)**
```bash
npx azurite --silent --location /tmp/azurite
```

**Terminal 2 — .NET API**
```bash
cd src/AzureJobOrchestrator.Api
dotnet run
# API: http://localhost:5000
# Swagger: http://localhost:5000/swagger
```

The API seeds 6 sample jobs and 2 sample pipelines on first startup.

**Terminal 3 — React frontend**
```bash
cd web
npm install
npm run dev
# UI: http://localhost:5173
```

**Optional — Azure Functions**
```bash
cd src/AzureJobOrchestrator.Functions
func start
# Functions: http://localhost:7071
```

### Simulation Mode

Both executors default to simulation mode when real Azure credentials are absent:

- **Batch simulator** — waits `Batch:SimulationDurationSeconds` (default 10s), then randomly succeeds (90%) or fails (10%)
- **Functions simulator** — waits `Functions:SimulationDurationSeconds` (default 6s), then succeeds

---

## 11. Working Session Guide — Real-World Examples

This section describes how to wire each template to real Azure resources for a live demo or proof-of-concept session.

---

### Example 1 — Database Trigger via Service Bus

**Scenario:** A SQL job runs whenever a record is inserted into a staging table. An application publishes a Service Bus message on insert; the orchestrator picks it up and fires a stored procedure job.

**Setup:**

1. **Create a job** with:
   - Target: Azure Functions
   - Template: `StoredProcedure`
   - Trigger: Service Bus
   - Parameters: `keyVaultSecretName = "SqlConnectionString"`, `storedProcedureName = "dbo.usp_ProcessStagingRecord"`

2. **Add the secret to Key Vault:**
   ```bash
   az keyvault secret set --vault-name <vault> --name SqlConnectionString \
     --value "Server=tcp:<server>.database.windows.net;Database=<db>;Authentication=Active Directory Managed Identity"
   ```

3. **Publisher (your application)** sends a Service Bus message on record insert:
   ```json
   { "action": "TriggerJob", "targetId": "<job-id>", "payloadJson": "{\"recordId\": 42}" }
   ```

4. The `ServiceBusListenerService` picks it up and fires the job. The stored procedure receives `previousStepOutput` (empty for the first step) and `recordId = 42` in its parameters.

**End result:** Every database insert triggers a stored procedure within seconds, fully observable in the Run History drawer.

---

### Example 2 — Stored Procedure Report Generation (Scheduled)

**Scenario:** Every weekday at 07:00 UTC, generate a sales summary report by calling a SQL stored procedure and emailing the results.

**Setup:**

1. **Create Job A — Generate Report:**
   - Target: Azure Functions
   - Template: `StoredProcedure`
   - Trigger: Cron → `0 7 * * MON-FRI`
   - Parameters: `storedProcedureName = "dbo.usp_GenerateSalesSummary"`, `commandTimeoutSeconds = 120`

2. **Create Job B — Send Email Report:**
   - Target: Azure Functions
   - Template: `HttpCall`
   - Trigger: Manual (fired by pipeline)
   - Parameters: `url = "https://api.sendgrid.com/v3/mail/send"`, `authType = "Bearer"`, `authKeyVaultSecretName = "SendGridApiKey"`, `method = "POST"`

3. **Create a Pipeline: "Sales Summary"**
   - Step 1: Job A (Generate Report)
   - Step 2: Job B (Send Email)

4. The pipeline fires at 07:00 UTC. Step 1's `outputPath` and `reportId` are injected into Step 2's parameters as `previousStepOutput`, allowing the email body to include the report reference.

---

### Example 3 — Blob Archival (Monthly Cleanup)

**Scenario:** On the first of every month, archive all log files older than 90 days from the `raw-logs` container to cool tier, then delete files older than 365 days.

**Setup:**

1. **Create Job A — Archive old logs:**
   - Target: Azure Batch (potentially thousands of files)
   - Template: `BlobOperation`
   - Trigger: Cron → `0 1 1 * *` (1st of month at 01:00 UTC)
   - Parameters: `containerName = "raw-logs"`, `operation = "Archive"`, `retentionDays = 90`, `fileExtension = "log"`

2. **Create Job B — Delete very old logs:**
   - Target: Azure Batch
   - Template: `BlobOperation`
   - Trigger: Manual (fired by pipeline)
   - Parameters: `containerName = "raw-logs"`, `operation = "Delete"`, `retentionDays = 365`, `fileExtension = "log"`

3. **Create a Pipeline: "Monthly Log Cleanup"**
   - Step 1: Job A (Archive 90-day-old logs)
   - Step 2: Job B (Delete 365-day-old logs)

4. The pipeline runs automatically on the first of each month. Step 1's output (bytes freed, file count) flows into Step 2's context. The dashboard tracks success/failure of both steps.

---

### Example 4 — HTTP Webhook → Data Validation → Report

**Scenario:** An external system posts to the HTTP trigger endpoint when a data pipeline completes. The orchestrator validates the output data and emails a quality report.

**Setup:**

1. **Create Job A — Validate Data:**
   - Target: Azure Functions
   - Template: `DataValidation`
   - Trigger: HTTP
   - Parameters: `dataset = "etl-output"`, `threshold = 0.98`, `notifyOnFailure = true`
   - Copy the trigger URL: `https://<api>/api/triggers/<jobId>/http`

2. **Create Job B — Generate Quality Report:**
   - Target: Azure Functions
   - Template: `ReportGeneration`
   - Trigger: Manual
   - Parameters: `template = "data-quality"`, `format = "html"`, `recipients = "data-team@example.com"`

3. **Create a Pipeline: "ETL Quality Gate"**
   - Step 1: Job A (Data Validation)
   - Step 2: Job B (Report Generation)
   - Set the Pipeline's trigger to HTTP by using the pipeline's own HTTP trigger endpoint

4. **External system call:**
   ```bash
   curl -X POST https://<api>/api/triggers/<pipelineId>/http \
     -H "X-Trigger-Token: <your-token>" \
     -H "Content-Type: application/json" \
     -d '{"dataset": "etl-output-2024-01-15"}'
   ```

5. Step 1 runs validation. Its quality score flows into Step 2 via `previousStepOutput`. The report email includes the actual quality metrics from Step 1.

---

### Example 5 — Service Bus → Document Processing Pipeline

**Scenario:** Documents uploaded to blob storage trigger a Service Bus message (via an Event Grid subscription). The orchestrator picks up the message and runs a 3-step pipeline: validate → OCR extract → store results.

**Setup:**

1. Create an Event Grid subscription on the blob container with a Service Bus topic output.

2. **Create a 3-step pipeline:**
   - Step 1: `DataValidation` — validate the uploaded document exists and is non-empty
   - Step 2: `DocumentProcessing` — OCR extract from `incoming-docs` → `processed-docs`
   - Step 3: `StoredProcedure` — call `dbo.usp_StoreDocumentMetadata` with the extracted data

3. **Configure the pipeline's trigger:** Service Bus

4. When Event Grid fires, the message is received by `ServiceBusListenerService` and the pipeline starts automatically. Each step's output (document ID, page count, extracted text path) flows into the next step via `previousStepOutput`.

---

### Example 6 — Real-Time Dashboard via Polling

**Scenario:** You want to watch a long-running Batch job progress in real time during a demo.

1. Create a job: Target Batch, Template DataValidation, Trigger Manual
2. Set `Batch:SimulationDurationSeconds = 30` in `appsettings.json` for a visible demo window
3. Click "Run Now" from the Azure Batch tab
4. Watch the Status badge animate from **Pending** → **Running** → **Succeeded**
5. Open Run History to see the full log output and duration

For real Batch workloads, the `RunStatusPollerService` polls the Batch task state every 5 seconds and updates the UI automatically.

---

## 12. Architecture Reference

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  React 18 + TypeScript + MUI v6  (web/)                                     │
│  Dashboard │ Batch │ Functions │ Pipelines │ Service Bus │ Templates         │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │ @tanstack/react-query  (polls every 3–10s)                           │   │
│  └────────────────────────────────┬─────────────────────────────────────┘   │
└───────────────────────────────────┼─────────────────────────────────────────┘
                                    │ REST / JSON  (Vite proxy in dev)
┌───────────────────────────────────▼─────────────────────────────────────────┐
│  ASP.NET Core Web API  .NET 10  (AzureJobOrchestrator.Api)                  │
│                                                                             │
│  Controllers:  Jobs · Runs · Pipelines · ServiceBus · Triggers             │
│                                                                             │
│  Services:                                                                  │
│  ┌──────────────────────┐  ┌────────────────────┐  ┌──────────────────┐   │
│  │  JobRunnerService    │  │ PipelineRunner      │  │ TemplateExecutor │   │
│  │  BatchJobExecutor    │  │ Service             │  │ Registry         │   │
│  │  FunctionJobExecutor │  └────────────────────┘  └──────────────────┘   │
│  └──────────────────────┘                                                   │
│                                                                             │
│  Background Services:                                                       │
│  ┌──────────────────────┐  ┌────────────────────┐  ┌──────────────────┐   │
│  │ RunStatusPoller      │  │ CronScheduler      │  │ ServiceBus       │   │
│  │ (every 5s)           │  │ (every 30s)        │  │ Listener         │   │
│  └──────────────────────┘  └────────────────────┘  └──────────────────┘   │
│                                                                             │
│  Repositories (Azure Table Storage):                                        │
│  JobDefinitions │ JobRuns │ PipelineDefinitions │ PipelineRuns              │
│                                                                             │
│  Secrets:  ISecretProvider → ConfigurationSecretProvider | KeyVaultProvider │
└────┬──────────────────────────┬──────────────────────────┬──────────────────┘
     │                          │                          │
     ▼                          ▼                          ▼
┌──────────────┐   ┌───────────────────────┐   ┌──────────────────────────┐
│ Azure Batch  │   │  Azure Functions .NET │   │  Azure Service Bus       │
│              │   │  10 (isolated worker) │   │  (or in-memory channel)  │
│ Pool → Task  │   │                       │   └──────────────────────────┘
│ → BatchWorker│   │  /api/DataValidation  │
│   console    │   │  /api/ReportGeneration│   ┌──────────────────────────┐
└──────────────┘   └───────────────────────┘   │  Azure Key Vault         │
                                               │  (or appsettings Secrets)│
     ▼                          ▼              └──────────────────────────┘
┌──────────────────────────────────────────┐
│  Azure Table Storage (or Azurite local)  │
│  JobDefinitions  ·  JobRuns              │
│  PipelineDefinitions  ·  PipelineRuns    │
└──────────────────────────────────────────┘
```

---

## 13. API Reference

### Jobs

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/jobs?target=Batch\|Function` | List all jobs for a target |
| `POST` | `/api/jobs` | Create a new job |
| `PUT` | `/api/jobs/{id}` | Update a job |
| `PATCH` | `/api/jobs/{id}/enabled` | Enable or disable a job |
| `DELETE` | `/api/jobs/{id}` | Delete a job |
| `POST` | `/api/jobs/{id}/run` | Trigger a job run |
| `GET` | `/api/jobs/{id}/runs` | List all runs for a job |

### Runs

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/runs/{runId}` | Get a single run |
| `POST` | `/api/runs/{runId}/rerun` | Rerun with original parameters |

### Pipelines

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/pipelines` | List all pipelines |
| `POST` | `/api/pipelines` | Create a pipeline |
| `PUT` | `/api/pipelines/{id}` | Update a pipeline |
| `PATCH` | `/api/pipelines/{id}/enabled` | Enable or disable |
| `DELETE` | `/api/pipelines/{id}` | Delete a pipeline |
| `POST` | `/api/pipelines/{id}/run` | Trigger a pipeline run |
| `GET` | `/api/pipelines/{id}/runs` | List all runs for a pipeline |
| `GET` | `/api/pipelines/runs/{runId}` | Get a single pipeline run |

### Service Bus

| Method | Path | Description |
|---|---|---|
| `POST` | `/api/servicebus/messages` | Send a message to the queue |
| `GET` | `/api/servicebus/messages` | List recent messages |

### HTTP Trigger

| Method | Path | Description |
|---|---|---|
| `POST` | `/api/triggers/{jobId}/http` | Fire a job via HTTP; pass `X-Trigger-Token` header if configured |
| `GET` | `/api/triggers/{jobId}/http` | Get the trigger URL and config info for a job |

---

*Azure Job Orchestrator — .NET 10 · React 18 · MUI v6*
*Authored by Ravindra Devkhile*
