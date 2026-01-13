---
layout: default
---

# Dapr workflow example

```csharp
var tasks = new List<Task<Result>>
{
    context.CallActivityAsync<Result>(
        nameof(SubTask1), input),
    context.CallActivityAsync<Result>(
        nameof(SubTask2), input),
    context.CallActivityAsync<Result>(
        nameof(SubTask3), input)
};

var results = await Task.WhenAll(tasks);

var summary = await context.CallActivityAsync<Summary>(
    nameof(AggregateResultsActivity), results);

return summary;
```
