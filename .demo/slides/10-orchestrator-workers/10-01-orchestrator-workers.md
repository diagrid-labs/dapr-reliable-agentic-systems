---
layout: section
customTheme: .demo/slides/theme/theme.css
---

# Orchestrator-Workers

Central orchestrator dynamically decomposes tasks and delegates to workers

![Animation](.demo/images/bot-animations-1.gif)

---
layout: default
customTheme: .demo/slides/theme/theme.css
---

# Orchestrator-Workers

## When to Use

- Subtasks can't be predicted upfront
- Dynamic decomposition needed
- Complex, unpredictable task structures

## Use Cases

- Coding products (multi-file changes)
- Research tasks
- Complex planning

---
layout: default
customTheme: .demo/slides/theme/theme.css
---

# Orchestrator-Workers

## Pros ✅

- Flexible and adaptable
- Scales to complex problems
- Clear separation of concerns

## Cons ❌

- Higher complexity
- Higher latency and cost
- High orchestrator dependency

---
layout: default
customTheme: .demo/slides/theme/theme.css
---

# Orchestrator-Workers using Dapr workflow

```csharp
// Orchestrator decomposes task
var instructions = await context.CallActivityAsync<List<Instruction>>(
    nameof(DecomposeActivity), input);

// Assign to workers
var workerTasks = new List<Task<Result>>();
foreach (var instruction in instructions)
{
    workerTasks.Add(context.CallActivityAsync<Result>(
        nameof(WorkerActivity), instruction));
}

var results  = await Task.WhenAll(workerTasks);

// Consolidate results
return await context.CallActivityAsync<Output>(
    nameof(ConsolidateActivity), results);
```
