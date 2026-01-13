---
layout: default
---

# Dapr workflow example

```csharp
var state = input;
var maxSteps = 10;

for (int step = 0; step < maxSteps; step++)
{
    var decision = await context.CallActivityAsync<Decision>(
        nameof(ReasoningActivity), state);
    
    if (decision.IsGoalAchieved)
        break;
    
    var actionResult = await context.CallActivityAsync<Result>(
        decision.SelectedTool, decision.ToolInput);
    
    state = await context.CallActivityAsync<State>(
        nameof(UpdateStateActivity), actionResult);
}
```

