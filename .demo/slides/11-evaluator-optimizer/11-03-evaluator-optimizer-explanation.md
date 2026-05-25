---
layout: default
---

# Evaluator-Optimizer

## When to Use

- Iterative refinement adds value
- Clear evaluation criteria exist

## Use Cases

- Content quality improvement
- Code generation with validation

---

# Evaluator-Optimizer with Dapr workflow

```csharp
double threshold = 0.8;
int maxIterations = 5;

var current = await context.CallActivityAsync<string>(
    nameof(GenerateActivity), input);

var quality = await context.CallActivityAsync<double>(
    nameof(EvaluateActivity), current);

if (quality >= threshold || iteration >= maxIterations)
{
    return current.Result;
}

current = await context.CallActivityAsync<string>(
    nameof(OptimizeActivity), current);
current.Iteration++;

context.ContinueAsNew(current);
```
