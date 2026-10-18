using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapr.AI.Conversation.Extensions;
using Dapr.Client;
using Dapr.Workflow;
using Dapr.Workflow.Versioning;
using SpaceDebrisAgent.Models;
using SpaceDebrisAgent.Workflows;
using SpaceDebrisAgent.Activities.Agent;
using SpaceDebrisAgent.Activities.Tools;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Configure JSON options to handle enum conversion
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddDaprClient();
builder.Services.AddDaprConversationClient((_, clientBuilder) =>
{
    clientBuilder.UseTimeout(TimeSpan.FromMinutes(4));
});
builder.Services.AddDaprWorkflow();
builder.Services.AddDaprWorkflowVersioning();

var app = builder.Build();

// Start autonomous cleanup mission
app.MapPost("/mission/start", async (
    [FromBody] MissionParameters mission,
    [FromServices] DaprWorkflowClient workflowClient) =>
{
    var instanceId = $"CLEAN-{mission.MissionId}";
    var input = new SpaceDebrisCleanupWorkflowInput(
        mission,
        null,
        new List<AgentDecision>(),
        new List<ToolCall>());
    
    await workflowClient.ScheduleNewWorkflowAsync(
        nameof(SpaceDebrisCleanupWorkflow),
        instanceId,
        input);
    
    return Results.Accepted($"/mission/status/{instanceId}", new { instanceId });
});

// Get mission status
app.MapGet("/mission/status/{instanceId}", async (
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
            ? state.ReadOutputAs<MissionResult>()
            : null,
        progress = ReadProgress(state),
        createdAt = state.CreatedAt,
        lastUpdatedAt = state.LastUpdatedAt
    });
});

// Get agent's decision history
app.MapGet("/mission/{instanceId}/decisions", async (
    string instanceId,
    [FromServices] DaprWorkflowClient workflowClient) =>
{
    var state = await workflowClient.GetWorkflowStateAsync(instanceId);
    
    if (!state.Exists)
        return Results.NotFound();
    
    var result = state.RuntimeStatus == WorkflowRuntimeStatus.Completed
        ? state.ReadOutputAs<MissionResult>()
        : null;
    return Results.Ok(result?.Decisions ?? new List<AgentDecision>());
});

// Send human approval to workflow
app.MapPost("/mission/{instanceId}/approval", async (
    string instanceId,
    [FromBody] HumanApproval approval,
    [FromServices] DaprWorkflowClient workflowClient) =>
{
    var state = await workflowClient.GetWorkflowStateAsync(instanceId);
    
    if (!state.Exists)
        return Results.NotFound();
    
    // Raise the HumanApproval event to the workflow
    await workflowClient.RaiseEventAsync(
        instanceId,
        "HumanApproval",
        approval);
    
    return Results.Ok(new { message = "Approval sent to workflow", approval });
});

// Instances persisted with an older, non-JSON-object custom status have no usable progress
static MissionProgress? ReadProgress(WorkflowState state)
{
    try { return state.ReadCustomStatusAs<MissionProgress>(); }
    catch (JsonException) { return null; }
}

app.MapDefaultEndpoints();

app.Run();
