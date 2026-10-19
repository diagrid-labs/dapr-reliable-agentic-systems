using System.Reflection;
using CommunityToolkit.Aspire.Hosting.Dapr;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddDapr();

string executingPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
    ?? throw new("Where am I?");

var cachePassword = builder.AddParameter("cache-password", "state-store-123", secret: true);

var cache = builder
    .AddValkey("cache", 16379, cachePassword)
    .WithContainerName("galactic-anomaly-classifier-state")
    .WithDataVolume("galactic-anomaly-classifier-state-data");

var workflowApp = builder
    .AddProject<Projects.GalacticAnomalyClassifier_ApiService>("wf-app")
    .WithDaprSidecar(new DaprSidecarOptions
    {
        AppId = "anomaly-routing-app",
        LogLevel = "debug",
        ResourcesPaths = [ Path.Join(executingPath, "Resources") ],
    });

workflowApp.WaitFor(cache);

builder
    .AddProject<Projects.GalacticAnomalyClassifier_Web>("web")
    .WithReference(workflowApp)
    .WaitFor(workflowApp)
    .WithExternalHttpEndpoints();

builder.AddExecutable("dapr-dev-dashboard", "diagrid-dev-dashboard", ".",
        "--port", "9090", "--no-open")
    .WithHttpEndpoint(port: 9090, isProxied: false)
    .WithUrlForEndpoint("http", url => url.Url = "http://localhost:9090/workflows");

builder.Build().Run();
