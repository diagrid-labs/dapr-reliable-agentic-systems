---
layout: default
customTheme: .demo/slides/theme/theme.css
---

# Parallelization

## Two Variations

- **Sectioning** - Break into independent subtasks
- **Voting** - Same task multiple times for consensus

## Use Cases

- Multi-source analysis
- Code review with voting

---

# Parallelization

## Pros ✅

- Higher throughput
- Higher confidence (voting)

## Cons ❌

- Tasks must be independent
- Requires aggregation logic

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
