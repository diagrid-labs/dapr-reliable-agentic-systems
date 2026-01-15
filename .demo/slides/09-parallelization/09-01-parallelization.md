---
layout: section
customTheme: .demo/slides/theme/theme.css
---

# Parallelization

Execute independent tasks concurrently

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
layout: default
customTheme: .demo/slides/theme/theme.css
---

# Parallelization

## Pros ✅

- Significantly reduced latency
- Higher confidence (voting)
- Better throughput

## Cons ❌

- Increased cost (multiple calls)
- Requires aggregation logic
- Tasks must be independent

---
layout: default
customTheme: .demo/slides/theme/theme.css
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