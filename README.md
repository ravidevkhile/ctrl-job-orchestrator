# Azure Job Orchestrator

A demo-grade proof-of-concept showing two ways to execute configurable batch jobs on Azure — **Azure Batch** and **Azure Functions** — controlled from a single React web UI.

## Architecture overview

```
┌─────────────────────────────────────────────────────────────────────┐
│  React 18 + MUI (web/)                                              │
│  Two tabs — Azure Batch │ Azure Functions                           │
└──────────────────────────────┬──────────────────────────────────────┘
                               │ HTTP / REST
┌──────────────────────────────▼──────────────────────────────────────┐
│  ASP.NET Core Web API — .NET 10   (AzureJobOrchestrator.Api)        │
│  ┌──────────────────┐   ┌──────────────────────────────────────┐    │
│  │  JobsController  │   │  RunsController                      │    │
│  └──────────────────┘   └──────────────────────────────────────┘    │
│  ┌───────────────────────────────────────────────────────────────┐  │
│  │  JobRunnerService  →  BatchJobExecutor / FunctionJobExecutor  │  │
│  └───────────────────────────────────────────────────────────────┘  │
│  ┌───────────────────────────────────────────────────────────────┐  │
│  │  RunStatusPollerService (background)  — polls every 5s        │  │
│  └───────────────────────────────────────────────────────────────┘  │
│  ┌───────────────────┐   ┌──────────────────┐                       │
│  │  IJobRepository   │   │  IRunRepository  │  (Table Storage)      │
│  └───────────────────┘   └──────────────────┘                       │
└──────────────────────────┬─────────────────────┬────────────────────┘
                           │                     │
          ┌────────────────▼──────┐   ┌──────────▼──────────────────┐
          │  Azure Batch          │   │  Azure Functions (.NET 10)   │
          │  (or simulation mode) │   │  AzureJobOrchestrator.       │
          │                       │   │  Functions                   │
          └───────────────────────┘   └──────────────────────────────┘
```

## Prerequisites

| Tool | Version | Notes |
|---|---|---|
| .NET SDK | 10 | `dotnet --version` should show `10.x` |
| Azure Functions Core Tools | v4 (with .NET 10 isolated support) | `func --version` |
| Node.js | 20 LTS or later | `node --version` |
| Azurite | latest | Storage emulator for local dev |

### Install Azurite

```bash
npm install -g azurite
```

### Install Azure Functions Core Tools v4

```bash
# macOS (Homebrew)
brew tap azure/functions
brew install azure-functions-core-tools@4

# Or npm
npm install -g azure-functions-core-tools@4 --unsafe-perm true
```

---

## Local development

### 1 — Start Azurite (Table + Blob emulator)

```bash
azurite --silent --location /tmp/azurite --debug /tmp/azurite-debug.log
```

Azurite listens on:
- Blob: `http://127.0.0.1:10000`
- Queue: `http://127.0.0.1:10001`
- Table: `http://127.0.0.1:10002`

The API and Functions project use the default connection string `UseDevelopmentStorage=true` when running locally.

### 2 — Start the API

```bash
cd src/AzureJobOrchestrator.Api
dotnet run
```

The API starts on `http://localhost:5000` / `https://localhost:5001`.

On the first startup it **seeds 6 sample jobs** (3 Batch, 3 Function) into Azurite so the UI is not empty.

Swagger UI: `https://localhost:5001/swagger`

#### Key configuration (`appsettings.Development.json`)

```json
{
  "Batch": {
    "SimulationMode": true,      // ← no real Batch account needed locally
    "SimulationDurationSeconds": 10
  },
  "Functions": {
    "SimulationDurationSeconds": 5   // ← no real Function endpoints needed
  }
}
```

When `Batch:SimulationMode` is `true` the executor mimics task execution with a delay. The Batch status badge transitions **Pending → Running → Succeeded/Failed** in the UI without any Batch infrastructure.

When `Functions:Endpoints:{JobType}` is not configured the Function executor also runs in simulation mode automatically.

### 3 — Start the Azure Functions project

```bash
cd src/AzureJobOrchestrator.Functions
func start
```

Functions listen on `http://localhost:7071`.

To wire a real function endpoint from the API, add to `appsettings.Development.json`:

```json
"Functions": {
  "Endpoints": {
    "DataValidation": "http://localhost:7071/api/DataValidation?code=<key>",
    "ReportGeneration": "http://localhost:7071/api/ReportGeneration?code=<key>"
  }
}
```

### 4 — Start the React frontend

```bash
cd web
cp .env.example .env.local    # optional — Vite proxy handles /api in dev
npm install
npm run dev
```

Open **http://localhost:5173** in your browser.

The Vite dev proxy forwards all `/api/*` requests to `http://localhost:5000` so there is no CORS issue in local development.

---

## How simulation mode works

| Mode | Batch | Functions |
|---|---|---|
| **Local dev (default)** | Fake task with configurable delay | Fake invocation with configurable delay |
| **Real Azure** | Submits actual Batch task via SDK | Posts HTTP to the Function endpoint |

The background `RunStatusPollerService` polls every 5 seconds and advances a simulated run's status from **Running** → **Succeeded** (or **Failed**, ~10% of the time, to make the demo interesting). The UI polls `GET /runs/{id}` every 5 seconds independently.

---

## Execution paths (inline comments)

### Azure Batch path

1. `JobsController.RunNow` → `JobRunnerService.TriggerAsync`
2. Creates a `JobRun` with `Status=Pending`, persists to Table Storage.
3. Calls `BatchJobExecutor.StartAsync`:
   - **Simulation:** records start timestamp in `ExternalId`, returns immediately.
   - **Real:** uses `BatchSharedKeyCredentials`, creates a Batch job + task, uploads the `worker.exe` resource file, returns the task id.
4. Sets `Status=Running`, persists.
5. `RunStatusPollerService` (background) calls `BatchJobExecutor.PollAsync` every 5s:
   - **Simulation:** checks elapsed seconds, transitions to terminal state after `SimulationDurationSeconds`.
   - **Real:** calls `batchClient.JobOperations.GetTaskAsync`, reads `TaskState.Completed` + exit code.
6. Updates `JobRun.Status`, `CompletedAt`, `Log`, `OutputLocation`; syncs `JobDefinition.LastRunStatus`.

### Azure Functions path

1. Same as steps 1–2 above.
2. Calls `FunctionJobExecutor.StartAsync`:
   - **Simulation:** records start timestamp, same pattern as Batch sim.
   - **Real:** HTTP POST to the configured function endpoint, captures `x-ms-invocation-id` from the response header; marks the run terminal immediately (synchronous HTTP wait).
3. Poll loop advances simulated runs to terminal state.

---

## Job types

| Key | Batch worker logic | Function endpoint |
|---|---|---|
| `DataValidation` | Validates a dataset, returns a quality score | `POST /api/DataValidation` |
| `ReportGeneration` | Generates a PDF/HTML report | `POST /api/ReportGeneration` |
| `DocumentProcessing` | Processes documents with optional OCR | _(simulation only)_ |

---

## Azure deployment (.NET 10 specifics)

> See the companion `02-azure-resources.md` and `provision-azure-resources.sh` for full provisioning.

### Function App

**.NET 10 is not supported on Linux Consumption.** Deploy to:
- **Flex Consumption plan** (recommended) — set `kind=functionapp,linux`, `sku=FlexConsumption`
- **Windows Consumption plan** — set `os_type=Windows`

Required app settings:
```
FUNCTIONS_WORKER_RUNTIME=dotnet-isolated
AzureWebJobsStorage=<connection-string-or-managed-identity>
```

### API Web App

Linux App Service, runtime `DOTNETCORE:10.0`.

```bash
dotnet publish src/AzureJobOrchestrator.Api -c Release -o ./publish/api
az webapp deploy --resource-group <rg> --name <api-app> --src-path ./publish/api
```

### Functions publish

```bash
cd src/AzureJobOrchestrator.Functions
func azure functionapp publish <function-app-name>
```

---

## Project structure

```
AzureJobOrchestrator/
├─ src/
│  ├─ AzureJobOrchestrator.Core/          Shared models, enums, DTOs
│  ├─ AzureJobOrchestrator.Api/           ASP.NET Core Web API (control plane)
│  │  ├─ Controllers/                     JobsController, RunsController
│  │  ├─ Services/                        IJobExecutor, BatchJobExecutor, FunctionJobExecutor, JobRunnerService
│  │  ├─ Repositories/                    IJobRepository, IRunRepository, TableStorage*
│  │  └─ BackgroundServices/              RunStatusPollerService
│  ├─ AzureJobOrchestrator.Functions/     Azure Functions (.NET 10 isolated worker)
│  │  └─ Functions/                       SampleDataValidationFunction, SampleReportFunction
│  └─ AzureJobOrchestrator.BatchWorker/   Console app run as the Azure Batch task
├─ web/                                   React 18 + TypeScript + MUI
│  └─ src/
│     ├─ api/                             Typed axios API client
│     ├─ components/                      JobsTable, JobFormDialog, RunHistoryDrawer, StatusBadge
│     └─ tabs/                            BatchTab, FunctionsTab
├─ Directory.Packages.props               Central Package Management (all version pins)
└─ AzureJobOrchestrator.sln
```

---

*Authored by Ravindra Devkhile. Targets .NET 10 (LTS, GA November 2025).*
