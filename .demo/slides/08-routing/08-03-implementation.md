---
layout: default
---

## Implementation

### Code Example

```csharp
var classification = await context.CallActivityAsync<string>(
    nameof(ClassifyActivity), input);

if (classification == "TypeA")
{
    result = await context.CallActivityAsync<Result>(
        nameof(HandleTypeA), input);
}
else if (classification == "TypeB")
{
    result = await context.CallActivityAsync<Result>(
        nameof(HandleTypeB), input);
}
```

### Pattern

1. Classify → 2. Route → 3. Handle
