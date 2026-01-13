---
layout: default
---

# Dapr workflow example

```csharp
// Orchestrator decomposes task
var tasks = await context.CallActivityAsync<List<Task>>(
    nameof(DecomposeActivity), input);

// Assign to workers
var results = new List<Result>();
foreach (var task in tasks)
{
    var result = await context.CallActivityAsync<Result>(
        nameof(WorkerActivity), task);
    results.Add(result);
}

// Consolidate results
return await context.CallActivityAsync<Output>(
    nameof(ConsolidateActivity), results);
```
