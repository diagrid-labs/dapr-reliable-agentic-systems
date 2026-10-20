using System.Reflection;
using CommunityToolkit.Aspire.Hosting.Dapr;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddDapr();

string executingPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
    ?? throw new("Where am I?");

var wfStatePassword = builder.AddParameter("wf-state-password", "state-store-123", secret: true);

var wfState = builder
    .AddValkey("wf-state", 16379, wfStatePassword)
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

workflowApp.WaitFor(wfState);

builder
    .AddProject<Projects.SpaceDebrisAgent_Web>("web")
    .WithReference(workflowApp)
    .WaitFor(workflowApp)
    .WithExternalHttpEndpoints();

builder.AddExecutable("dapr-dev-dashboard", "diagrid-dev-dashboard", ".",
        "--port", "9090", "--no-open")
    .WithHttpEndpoint(port: 9090, isProxied: false)
    .WithUrlForEndpoint("http", url => url.Url = "http://localhost:9090/workflows");

builder.Build().Run();
