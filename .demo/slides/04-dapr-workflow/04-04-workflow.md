---
layout: default
---

## Workflow example

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