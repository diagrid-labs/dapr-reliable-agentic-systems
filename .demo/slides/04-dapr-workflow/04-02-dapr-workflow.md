---
layout: default
customTheme: .demo/slides/theme/theme.css
---

# Dapr Workflow

- Dapr sidecar contains a workflow engine, inspired by the Durable Task Framework.
- Workflows are defined in code, using familiar programming constructs.
- Workflows are stateful, and should be deterministic.
- Activities contain non-deterministic code.

---
layout: default
customTheme: .demo/slides/theme/theme.css
---

# Workflow Management example

```csharp
app.MapPost("/start", async (
    [FromBody] Input input,
    [FromServices] DaprWorkflowClient workflowClient) =>
{
    var instanceID = await workflowClient.ScheduleNewWorkflowAsync(
        nameof(MyWorkflow),
        input);
    
    return Results.Accepted($"/start/{instanceID}", new { instanceID });
});

```

---
layout: default
customTheme: .demo/slides/theme/theme.css
---

# Workflow example

```csharp
public class MyWorkflow : Workflow<Input, Output>
{
    public override async Task<Output> RunAsync(
        WorkflowContext context, Input input)
    {
        var activity1Result = await context.CallActivityAsync<string>(
            nameof(MyActivity1), input);
        
        var result = await context.CallActivityAsync<string>(
            nameof(MyActivity2), activity1Result);

        return new Output(result);
    }
}
```

---
layout: default
customTheme: .demo/slides/theme/theme.css
---

# Workflow Activity example

```csharp
public class MyActivity1 : Activity<Input, string>
{
    public override async Task<string> RunAsync(
        ActivityContext context, Input input)
    {
        var response = await CallLLMAsync(input);
        return response;
    }
}
```

