using Dapr.Client;
using Dapr.Workflow;
using Dapr.AI.Conversation.Extensions;
using GalacticAnomalyClassifier.Models;
using GalacticAnomalyClassifier.Workflows;
using GalacticAnomalyClassifier.Activities;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDaprClient();
builder.Services.AddDaprConversationClient();
builder.Services.AddDaprWorkflow(options =>
{
    options.RegisterWorkflow<AnomalyRoutingWorkflow>();
    options.RegisterActivity<ClassifyAnomalyActivity>();
    options.RegisterActivity<ResponseCleanupActivity>();
    options.RegisterActivity<AnalyzeTemporalRiftActivity>();
    options.RegisterActivity<AnalyzeDarkMatterActivity>();
    options.RegisterActivity<AnalyzeAlienArtifactActivity>();
    options.RegisterActivity<AnalyzeStellarPhenomenonActivity>();
    options.RegisterActivity<AnalyzeDimensionalTearActivity>();
});

var app = builder.Build();

app.MapPost("/anomaly/analyze", async (
    SpaceAnomaly anomaly,
    DaprWorkflowClient workflowClient,
    DaprClient daprClient) =>
{
    await daprClient.SaveStateAsync(
        "statestore", 
        anomaly.AnomalyId, 
        anomaly);
    
    var instanceId = await workflowClient.ScheduleNewWorkflowAsync(
        nameof(AnomalyRoutingWorkflow),
        anomaly.AnomalyId,
        anomaly);
    
    return Results.Accepted($"/anomaly/status/{instanceId}", new { instanceId });
});

app.MapGet("/anomaly/status/{instanceId}", async (
    string instanceId,
    DaprWorkflowClient workflowClient) =>
{
    var state = await workflowClient.GetWorkflowStateAsync(instanceId);
    
    if (state == null)
        return Results.NotFound();
    
    return Results.Ok(new
    {
        instanceId,
        status = state.RuntimeStatus.ToString(),
        result = state.ReadOutputAs<AnalysisResult>(),
        createdAt = state.CreatedAt,
        lastUpdatedAt = state.LastUpdatedAt
    });
});

app.MapGet("/anomalies/{anomalyId}", async (
    string anomalyId,
    DaprClient daprClient) =>
{
    var result = await daprClient.GetStateAsync<AnalysisResult>(
        "statestore",
        anomalyId);
    
    if (result == null)
        return Results.NotFound();
    
    return Results.Ok(result);
});

app.MapGet("/anomalies/stats", (DaprClient daprClient) =>
{
    return Results.Ok(new
    {
        total = 42,
        byType = new Dictionary<string, int>
        {
            ["TemporalRift"] = 8,
            ["DarkMatterCluster"] = 15,
            ["AlienArtifact"] = 5,
            ["StellarPhenomenon"] = 12,
            ["DimensionalTear"] = 2
        }
    });
});

app.Run();
