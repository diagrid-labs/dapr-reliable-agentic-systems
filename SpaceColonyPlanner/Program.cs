using Dapr.AI.Conversation.Extensions;
using Dapr.Client;
using Dapr.Workflow;
using SpaceColonyPlanner.Models;
using SpaceColonyPlanner.Workflows;
using SpaceColonyPlanner.Activities.Analysis;
using SpaceColonyPlanner.Activities.Workers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

builder.Services.AddDaprClient();
builder.Services.AddDaprConversationClient();
builder.Services.AddDaprWorkflow(options =>
{
    options.RegisterWorkflow<ColonyOrchestratorWorkflow>();
    
    // Analysis activities
    options.RegisterActivity<AnalyzePlanetActivity>();
    options.RegisterActivity<DetermineStructuresActivity>();
    options.RegisterActivity<SynthesizePlanActivity>();
    options.RegisterActivity<OptimizeTimelineActivity>();
    
    // Worker activities
    options.RegisterActivity<PlanHabitatDomeActivity>();
    options.RegisterActivity<PlanPowerPlantActivity>();
    options.RegisterActivity<PlanAgricultureActivity>();
    options.RegisterActivity<PlanMiningFacilityActivity>();
    options.RegisterActivity<PlanResearchLabActivity>();
    options.RegisterActivity<PlanDefenseSystemActivity>();
    options.RegisterActivity<UnknownStructureActivity>();
});

var app = builder.Build();

// Plan a new colony
app.MapPost("/colony/plan", async (
    ColonyRequest request,
    DaprWorkflowClient workflowClient,
    DaprClient daprClient) =>
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
    DaprWorkflowClient workflowClient) =>
{
    var state = await workflowClient.GetWorkflowStateAsync(instanceId);
    
    if (state == null)
        return Results.NotFound();
    
    return Results.Ok(new
    {
        instanceId,
        status = state.RuntimeStatus.ToString(),
        plan = state.ReadOutputAs<ColonyMasterPlan>(),
        createdAt = state.CreatedAt,
        lastUpdatedAt = state.LastUpdatedAt
    });
});

app.Run();
