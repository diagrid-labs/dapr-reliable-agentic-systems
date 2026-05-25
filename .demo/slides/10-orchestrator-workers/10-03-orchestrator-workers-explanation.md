---
layout: default
---

# Orchestrator-Workers

## When to Use

- Dynamic decomposition needed (different subtasks per task)
- Complex, unpredictable task structures

## Use Cases

- Coding agents (multi-file changes)
- Research tasks
- Complex planning

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
