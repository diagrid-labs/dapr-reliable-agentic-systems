using Dapr.AI.Conversation.Extensions;
using Dapr.Client;
using Dapr.Workflow;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddDaprClient();
builder.Services.AddDaprConversationClient();
builder.Services.AddDaprWorkflow(options =>
{
    options.RegisterWorkflow<AlienTranslationWorkflow>();
    options.RegisterActivity<TranslateActivity>();
    options.RegisterActivity<RefineTranslationActivity>();
    options.RegisterActivity<EvaluateTranslationActivity>();
});

var app = builder.Build();

app.MapPost("/translate", async (
    AlienText text,
    DaprWorkflowClient workflowClient,
    DaprClient daprClient) =>
{
    var instanceId = $"translation-{text.TextId}";
    
    await daprClient.SaveStateAsync(
        "statestore",
        $"text-{text.TextId}",
        text);
    
    await workflowClient.ScheduleNewWorkflowAsync(
        nameof(AlienTranslationWorkflow),
        instanceId,
        text);
    
    return Results.Accepted($"/translate/{instanceId}", new { instanceId });
});

app.MapGet("/translate/{instanceId}", async (
    string instanceId,
    DaprWorkflowClient workflowClient) =>
{
    var state = await workflowClient.GetWorkflowStateAsync(instanceId);
    
    if (state == null)
        return Results.NotFound();
    
    var result = state.ReadOutputAs<AlienTranslationWorkflowOutput>();
    
    return Results.Ok(new
    {
        instanceId,
        status = state.RuntimeStatus.ToString(),
        result,
        createdAt = state.CreatedAt,
        completedAt = state.LastUpdatedAt
    });
});

app.MapGet("/translate/{instanceId}/iteration/{iterationNumber}", async (
    string instanceId,
    int iterationNumber,
    DaprWorkflowClient workflowClient) =>
{
    var state = await workflowClient.GetWorkflowStateAsync(instanceId);
    
    if (state == null || state.ReadOutputAs<AlienTranslationWorkflowOutput>() == null)
        return Results.NotFound();
    
    var result = state.ReadOutputAs<AlienTranslationWorkflowOutput>()!;
    var evaluation = result.Evaluations.FirstOrDefault(e => e.IterationNumber == iterationNumber);
    
    if (evaluation == null)
        return Results.NotFound();
    
    return Results.Ok(new
    {
        evaluation
    });
});

app.MapGet("/translations/{textId}", async (
    string textId,
    DaprClient daprClient) =>
{
    var translation = await daprClient.GetStateAsync<AlienText>(
        "statestore",
        textId);
    
    if (translation == null)
        return Results.NotFound();
    
    return Results.Ok(translation);
});

app.Run();
