using Dapr.AI.Conversation.Extensions;
using Dapr.Workflow;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

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
    DaprWorkflowClient workflowClient) =>
{
    var instanceId = $"translation-{text.TextId}";
    
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

app.Run();
