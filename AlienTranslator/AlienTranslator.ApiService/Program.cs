using Microsoft.AspNetCore.Mvc;
using Dapr.AI.Conversation.Extensions;
using Dapr.Workflow;
using Dapr.Workflow.Versioning;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddDaprConversationClient((_, clientBuilder) =>
{
    clientBuilder.UseTimeout(TimeSpan.FromMinutes(4));
});
builder.Services.AddDaprWorkflow();
builder.Services.AddDaprWorkflowVersioning();

var app = builder.Build();

app.MapPost("/translate", async (
    [FromBody] AlienText text,
    [FromServices] DaprWorkflowClient workflowClient) =>
{
    var instanceId = $"TRANS-{text.TextId}";
    
    var input = new AlienTranslationWorkflowInput(
        text,
        new List<Translation>(),
        new List<Evaluation>()
    );

    await workflowClient.ScheduleNewWorkflowAsync(
        nameof(AlienTranslationWorkflow),
        instanceId,
        input);
    
    return Results.Accepted($"/translate/{instanceId}", new { instanceId });
});

app.MapGet("/translate/{instanceId}", async (
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
            ? state.ReadOutputAs<AlienTranslationWorkflowOutput>()
            : null,
        progress = ReadProgress(state),
        createdAt = state.CreatedAt,
        lastUpdatedAt = state.LastUpdatedAt
    });
});

// Instances persisted with an older, non-JSON-object custom status have no usable progress
static TranslationProgress? ReadProgress(WorkflowState state)
{
    try { return state.ReadCustomStatusAs<TranslationProgress>(); }
    catch (JsonException) { return null; }
}

app.MapDefaultEndpoints();

app.Run();
