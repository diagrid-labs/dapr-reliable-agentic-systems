using Microsoft.AspNetCore.Mvc;
using Dapr.Client;
using Dapr.Workflow;
using Dapr.Workflow.Versioning;
using Dapr.AI.Conversation.Extensions;
using GalacticAnomalyClassifier.Models;
using GalacticAnomalyClassifier.Workflows;
using GalacticAnomalyClassifier.Activities;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddDaprClient();
builder.Services.AddDaprConversationClient((_, clientBuilder) =>
{
    clientBuilder.UseTimeout(TimeSpan.FromMinutes(4));
});
builder.Services.AddDaprWorkflow();
builder.Services.AddDaprWorkflowVersioning();

var app = builder.Build();

app.MapPost("/anomaly/analyze", async (
    [FromBody] SpaceAnomaly anomaly,
    [FromServices] DaprWorkflowClient workflowClient,
    [FromServices] DaprClient daprClient) =>
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
    [FromServices] DaprWorkflowClient workflowClient) =>
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
    [FromServices] DaprClient daprClient) =>
{
    var result = await daprClient.GetStateAsync<AnalysisResult>(
        "statestore",
        anomalyId);
    
    if (result == null)
        return Results.NotFound();
    
    return Results.Ok(result);
});

app.MapGet("/anomalies/stats", ([FromServices] DaprClient daprClient) =>
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

app.MapDefaultEndpoints();

app.Run();
