using Microsoft.AspNetCore.Mvc;
using Dapr.Client;
using Dapr.Workflow;
using Dapr.AI.Conversation.Extensions;
using StarshipDiagnostics.Activities;
using StarshipDiagnostics.Activities.Scanners;
using StarshipDiagnostics.Activities.Voters;
using StarshipDiagnostics.Models;
using StarshipDiagnostics.Workflows;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDaprClient();
builder.Services.AddDaprConversationClient();
builder.Services.AddDaprWorkflow(options =>
{
    options.RegisterWorkflow<ParallelDiagnosticsWorkflow>();
    
    // Register scanner activities
    options.RegisterActivity<HullIntegrityScanActivity>();
    options.RegisterActivity<ReactorCoreScanActivity>();
    options.RegisterActivity<NavigationScanActivity>();
    options.RegisterActivity<LifeSupportScanActivity>();
    options.RegisterActivity<WeaponsScanActivity>();
    
    // Register voter activities
    options.RegisterActivity<SafetyVoterActivity>();
    options.RegisterActivity<SeverityVoterActivity>();
    options.RegisterActivity<RecommendationVoterActivity>();
    
    // Register aggregation
    options.RegisterActivity<AggregateResultsActivity>();
});

var app = builder.Build();

// Start diagnostic scan
app.MapPost("/ship/diagnose", async (
    [FromBody] Starship ship,
    [FromServices] DaprWorkflowClient workflowClient,
    [FromServices] DaprClient daprClient) =>
{
    var instanceId = $"DIAG-{ship.ShipId}-{DateTime.UtcNow.Ticks}";
    
    // Store ship data
    await daprClient.SaveStateAsync(
        "statestore", 
        $"ship-{ship.ShipId}", 
        ship);
    
    // Start parallel diagnostics workflow
    await workflowClient.ScheduleNewWorkflowAsync(
        nameof(ParallelDiagnosticsWorkflow),
        instanceId,
        ship);
    
    return Results.Accepted($"/ship/report/{instanceId}", new { instanceId });
});

// Get diagnostic report
app.MapGet("/ship/report/{instanceId}", async (
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
        result = state.ReadOutputAs<DiagnosticReport>(),
        createdAt = state.CreatedAt,
        lastUpdatedAt = state.LastUpdatedAt
    });
});

// Get ship maintenance history
app.MapGet("/ship/{shipId}/history", async (
    string shipId,
    [FromServices] DaprClient daprClient) =>
{
    // Query all diagnostics for this ship
    var shipData = await daprClient.GetStateAsync<Starship>(
        "statestore",
        $"ship-{shipId}");
    
    return Results.Ok(new
    {
        ship = shipData,
        lastScan = DateTime.UtcNow.AddDays(-3)
    });
});

app.Run();
