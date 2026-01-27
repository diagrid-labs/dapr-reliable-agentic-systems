using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapr.AI.Conversation.Extensions;
using Dapr.Client;
using Dapr.Workflow;
using SpaceDebrisAgent.Models;
using SpaceDebrisAgent.Workflows;
using SpaceDebrisAgent.Activities.Agent;
using SpaceDebrisAgent.Activities.Tools;

var builder = WebApplication.CreateBuilder(args);

// Configure JSON options to handle enum conversion
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddDaprClient();
builder.Services.AddDaprConversationClient();
builder.Services.AddDaprWorkflow(options =>
{
    options.RegisterWorkflow<SpaceDebrisCleanupWorkflow>();
    options.RegisterActivity<AgentReasoningActivity>();
    options.RegisterActivity<ScanDebrisFieldActivity>();
    options.RegisterActivity<AnalyzeDebrisActivity>();
    options.RegisterActivity<CaptureDebrisActivity>();
    options.RegisterActivity<MoveToLocationActivity>();
    options.RegisterActivity<CheckFuelActivity>();
    options.RegisterActivity<RequestHumanApprovalActivity>();
    options.RegisterActivity<GenerateReportActivity>();
});

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
    
    if (state == null)
        return Results.NotFound();
    
    return Results.Ok(new
    {
        instanceId,
        status = state.RuntimeStatus.ToString(),
        result = state.ReadOutputAs<MissionResult>(),
        createdAt = state.CreatedAt
    });
});

// Get agent's decision history
app.MapGet("/mission/{instanceId}/decisions", async (
    string instanceId,
    [FromServices] DaprWorkflowClient workflowClient) =>
{
    var state = await workflowClient.GetWorkflowStateAsync(instanceId);
    
    if (state == null)
        return Results.NotFound();
    
    var result = state.ReadOutputAs<MissionResult>();
    return Results.Ok(result.Decisions);
});

// Send human approval to workflow
app.MapPost("/mission/{instanceId}/approval", async (
    string instanceId,
    [FromBody] HumanApproval approval,
    [FromServices] DaprWorkflowClient workflowClient) =>
{
    var state = await workflowClient.GetWorkflowStateAsync(instanceId);
    
    if (state == null)
        return Results.NotFound();
    
    // Raise the HumanApproval event to the workflow
    await workflowClient.RaiseEventAsync(
        instanceId,
        "HumanApproval",
        approval);
    
    return Results.Ok(new { message = "Approval sent to workflow", approval });
});

app.Run();
