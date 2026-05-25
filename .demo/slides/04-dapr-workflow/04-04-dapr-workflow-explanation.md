---
layout: default
---

# Dapr Workflow

- Dapr has a built-in workflow engine, inspired by the Durable Task Framework.
- Workflows are defined in code.
- Workflows are stateful, and (should be) deterministic.
- Activities are the the building blocks of a workflow, they contain non-deterministic code.

```mermaid
%%{init: {"theme":"base","themeVariables":{"background":"#0a0a0a","primaryColor":"#1f3a32","primaryTextColor":"#e6e6e6","primaryBorderColor":"#41BD9B","lineColor":"#A6A6A6","secondaryColor":"#111315","tertiaryColor":"#111315","clusterBkg":"#111315","clusterBorder":"#41BD9B","titleColor":"#F0C75E","edgeLabelBackground":"#0a0a0a","noteBkgColor":"#111315","noteTextColor":"#e6e6e6","noteBorderColor":"#41BD9B","actorBkg":"#1f3a32","actorBorder":"#41BD9B","actorTextColor":"#e6e6e6","actorLineColor":"#A6A6A6","signalColor":"#A6A6A6","signalTextColor":"#e6e6e6","labelBoxBkgColor":"#0a0a0a","labelBoxBorderColor":"#41BD9B","labelTextColor":"#e6e6e6","loopTextColor":"#e6e6e6","altSectionBkgColor":"#111315","sectionBkgColor":"#111315","fontFamily":"Geist,sans-serif"}}}%%
graph LR
    subgraph Workflow
    direction LR
    Start([Input]) --> A1[Activity 1]
    A1 --> A2[Activity 2]
    A2 --> A3[Activity 3]
    A3 --> End([Output])
    end
    
    style Start fill:#41BD9B,stroke:#2d8a70,color:#0a0a0a
    style End fill:#41BD9B,stroke:#2d8a70,color:#0a0a0a
```

---

# Workflow start example

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

    private Task<string> CallLLMAsync(Input input)
    {
        // Call to external LLM service
        ...
    }
}
```

