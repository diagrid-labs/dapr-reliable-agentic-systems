using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Dapr.Workflow;
using Dapr.Workflow.Versioning;
using AnomalyAnalysis.Models;
using AnomalyAnalysis.Workflows;
using Dapr.AI.Conversation.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddDaprConversationClient((_, clientBuilder) =>
{
    clientBuilder.UseTimeout(TimeSpan.FromMinutes(4));
});
builder.Services.AddDaprWorkflow();
builder.Services.AddDaprWorkflowVersioning();

var app = builder.Build();

// Start analyzing a spatial anomaly
app.MapPost("/anomaly/analyze", async (
    [FromBody] SpatialAnomaly anomaly,
    [FromServices] DaprWorkflowClient workflowClient) =>
{
    var instanceId = $"ANOM-{anomaly.AnomalyId}";

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
    [FromServices] DaprWorkflowClient workflowClient) =>
{
    var state = await workflowClient.GetWorkflowStateAsync(instanceId);
    
    if (!state.Exists)
        return Results.NotFound();
    
    return Results.Ok(new
    {
        instanceId,
        status = state.RuntimeStatus.ToString(),
        result = state.RuntimeStatus == WorkflowRuntimeStatus.Completed
            ? state.ReadOutputAs<AnalysisResult>()
            : null,
        progress = ReadProgress(state),
        createdAt = state.CreatedAt,
        lastUpdatedAt = state.LastUpdatedAt
    });
});


// Instances persisted with an older, non-JSON-object custom status have no usable progress
static AnalysisProgress? ReadProgress(WorkflowState state)
{
    try { return state.ReadCustomStatusAs<AnalysisProgress>(); }
    catch (JsonException) { return null; }
}

app.MapDefaultEndpoints();

app.Run();
