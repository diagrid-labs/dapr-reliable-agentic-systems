using System.Reflection;
using CommunityToolkit.Aspire.Hosting.Dapr;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddDapr();

string executingPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
    ?? throw new("Where am I?");

var cachePassword = builder.AddParameter("cache-password", "state-store-123", secret: true);

var cache = builder
    .AddValkey("cache", 16379, cachePassword)
    .WithContainerName("space-debris-agent-state")
    .WithDataVolume("space-debris-agent-state-data");

var workflowApp = builder
    .AddProject<Projects.SpaceDebrisAgent_ApiService>("wf-app")
    .WithDaprSidecar(new DaprSidecarOptions
    {
        AppId = "space-debris-agent",
        LogLevel = "debug",
        ResourcesPaths = [ Path.Join(executingPath, "Resources") ],
    });

workflowApp.WaitFor(cache);

builder
    .AddContainer("diagrid-dashboard", "ghcr.io/diagridio/diagrid-dashboard:latest")
    .WithContainerName("diagrid-dashboard")
    .WithBindMount(Path.Join(executingPath, "Resources"), "/app/components")
    .WithEnvironment("COMPONENT_FILE", "/app/components/statestore-dashboard.yaml")
    .WithEnvironment("APP_ID", "diagrid-dashboard")
    .WithHttpEndpoint(port: 18080, targetPort: 8080)
    .WithReference(cache);

builder.Build().Run();
