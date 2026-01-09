---
layout: default
---

## Workflow Management example

```csharp
app.MapPost("/start", async (
    Input input,
    [FromServices] DaprWorkflowClient workflowClient) =>
{
    var instanceID = await workflowClient.ScheduleNewWorkflowAsync(
        nameof(MyWorkflow),
        input);
    
    return Results.Accepted($"/start/{instanceID}", new { instanceID });
});

```
