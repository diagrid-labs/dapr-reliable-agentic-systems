using Dapr.Client;
using Dapr.Workflow;
using AnomalyAnalysis.Models;
using AnomalyAnalysis.Workflows;
using AnomalyAnalysis.Activities;
using Dapr.AI.Conversation.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDaprClient();
builder.Services.AddDaprConversationClient();
builder.Services.AddDaprWorkflow(options =>
{
    options.RegisterWorkflow<AnomalyAnalysisWorkflow>();
    options.RegisterActivity<ProcessSensorDataActivity>();
    options.RegisterActivity<ClassifyAnomalyActivity>();
    options.RegisterActivity<ScientificAnalysisActivity>();
    options.RegisterActivity<RiskAssessmentActivity>();
    options.RegisterActivity<GenerateRecommendationActivity>();
    options.RegisterActivity<AlertBridgeActivity>();
});

var app = builder.Build();

// Start analyzing a spatial anomaly
app.MapPost("/anomaly/analyze", async (
    SpatialAnomaly anomaly,
    DaprWorkflowClient workflowClient,
    DaprClient daprClient) =>
{
    var instanceId = $"anomaly-{anomaly.AnomalyId}";
    
    // Store original anomaly data
    await daprClient.SaveStateAsync(
        "statestore", 
        $"anomaly-{anomaly.AnomalyId}", 
        anomaly);
    
    // Start workflow
    await workflowClient.ScheduleNewWorkflowAsync(
        nameof(AnomalyAnalysisWorkflow),
        instanceId,
        anomaly);
    
    return Results.Accepted($"/anomaly/status/{instanceId}", new { instanceId });
});

// Get analysis status
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

// Get a specific analyzed anomaly by ID
app.MapGet("/anomalies/{anomalyId}", async (
    string anomalyId,
    DaprClient daprClient) =>
{
    var anomaly = await daprClient.GetStateAsync<SpatialAnomaly>(
        "statestore",
        anomalyId);
    
    if (anomaly == null)
        return Results.NotFound();
    
    return Results.Ok(anomaly);
});

app.Run();
