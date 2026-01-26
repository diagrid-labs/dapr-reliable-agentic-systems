---
layout: default
customTheme: .demo/slides/theme/theme.css
---

# Autonomous Agent

## When to Use

- Open-ended problems
- Can't predict required steps
- Need maximum flexibility

## Use Cases

- Coding assistants
- Research agents

---

# Autonomous Agent with Dapr workflow

```csharp
const int maxIterations = 10;
//...

// Start of workflow
var decision = await context.CallActivityAsync<Decision>(
    nameof(ReasoningActivity), input);

// Exit criteria
if (decision.IsGoalAchieved || input.Iteration >= maxIterations) {
    return decision.Result;
}

var actionResult = await context.CallActivityAsync<Result>(
    decision.SelectedTool, decision.ToolInput);

input = await context.CallActivityAsync<State>(
    nameof(UpdateInputActivity), actionResult);

// Restart workflow with updated state and incremented step
context.ContinueAsNew(input);
```