---
layout: default
customTheme: .demo/slides/theme/theme.css
---

# Dapr workflow example

```csharp
double quality = 0.0;
double threshold = 0.8;
int maxIterations = 5;
// ...

// Workflow start

current = await context.CallActivityAsync<string>(
    nameof(GenerateActivity), input);

quality = await context.CallActivityAsync<double>(
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

