---
layout: default
---

# Dapr workflow example

```csharp
var current = input;
double quality = 0.0;
double threshold = 0.8;
int maxIterations = 5;

for (int i = 0; i < maxIterations; i++)
{
    current = await context.CallActivityAsync<string>(
        nameof(GenerateActivity), current);
    
    quality = await context.CallActivityAsync<double>(
        nameof(EvaluateActivity), current);
    
    if (quality >= threshold)
        break;
    
    current = await context.CallActivityAsync<string>(
        nameof(RefineActivity), current);
}
```

