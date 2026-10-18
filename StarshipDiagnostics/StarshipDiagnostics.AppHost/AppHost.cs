using System.Reflection;
using CommunityToolkit.Aspire.Hosting.Dapr;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddDapr();

string executingPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
    ?? throw new("Where am I?");

var cachePassword = builder.AddParameter("cache-password", "state-store-123", secret: true);

var cache = builder
    .AddValkey("cache", 16379, cachePassword)
    .WithContainerName("starship-diagnostics-state")
    .WithDataVolume("starship-diagnostics-state-data");

var workflowApp = builder
    .AddProject<Projects.StarshipDiagnostics_ApiService>("wf-app")
    .WithDaprSidecar(new DaprSidecarOptions
    {
        AppId = "starship-diagnostics-app",
        LogLevel = "debug",
        ResourcesPaths = [ Path.Join(executingPath, "Resources") ],
    });

workflowApp.WaitFor(cache);

builder.AddExecutable("dapr-dev-dashboard", "diagrid-dev-dashboard", ".",
        "--port", "9090", "--no-open")
    .WithHttpEndpoint(port: 9090, isProxied: false);

builder.Build().Run();
