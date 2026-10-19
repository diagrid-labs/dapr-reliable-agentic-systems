using Microsoft.AspNetCore.Mvc;
using Dapr.AI.Conversation.Extensions;
using Dapr.Client;
using Dapr.Workflow;
using Dapr.Workflow.Versioning;
using SpaceColonyPlanner.Models;
using SpaceColonyPlanner.Workflows;
using SpaceColonyPlanner.Activities.Analysis;
using SpaceColonyPlanner.Activities.Workers;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

builder.Services.AddDaprClient();
builder.Services.AddDaprConversationClient((_, clientBuilder) =>
{
    clientBuilder.UseTimeout(TimeSpan.FromMinutes(4));
});
builder.Services.AddDaprWorkflow();
builder.Services.AddDaprWorkflowVersioning();

var app = builder.Build();

// Plan a new colony
app.MapPost("/colony/plan", async (
    [FromBody] ColonyRequest request,
    [FromServices] DaprWorkflowClient workflowClient,
    [FromServices] DaprClient daprClient) =>
{
    var instanceId = $"COLO-{request.Planet.PlanetId}-{DateTime.UtcNow.Ticks}";
    
    // Save planet data
    await daprClient.SaveStateAsync(
        "statestore",
        $"planet-{request.Planet.PlanetId}",
        request.Planet);
    
    // Start orchestrator workflow
    await workflowClient.ScheduleNewWorkflowAsync(
        nameof(ColonyOrchestratorWorkflow),
        instanceId,
        request);
    
    return Results.Accepted($"/colony/plan/{instanceId}", new { instanceId });
});

// Get colony plan
app.MapGet("/colony/plan/{instanceId}", async (
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
            ? state.ReadOutputAs<ColonyMasterPlan>()
            : null,
        progress = state.ReadCustomStatusAs<ColonyProgress>(),
        createdAt = state.CreatedAt,
        lastUpdatedAt = state.LastUpdatedAt
    });
});

app.MapDefaultEndpoints();

app.Run();
