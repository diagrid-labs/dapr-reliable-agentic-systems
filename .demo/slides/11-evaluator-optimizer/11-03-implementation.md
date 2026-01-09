---
layout: default
---

## Implementation

### Code Example

```csharp
var current = input;
var quality = 0.0;
var maxIterations = 5;

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

### Guardrails Required

Always set maximum iterations
