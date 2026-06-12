using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// .NET 10 isolated worker Functions entry point.
// Uses the FunctionsApplication builder pattern with ASP.NET Core integration
// so HTTP-triggered functions have full middleware support.

var host = FunctionsApplication.CreateBuilder(args);

// Enable ASP.NET Core integration for HTTP-triggered functions.
host.ConfigureFunctionsWebApplication();

// Application Insights is configured via APPLICATIONINSIGHTS_CONNECTION_STRING
// environment variable — the SDK picks it up automatically when the package is
// referenced. No explicit registration needed with Worker 2.x + AI 2.x.

await host.Build().RunAsync();
