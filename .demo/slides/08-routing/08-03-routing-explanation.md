---
layout: default
---

# Routing

## When to Use

- Distinct categories better handled separately

## Use Cases

- Customer service routing
- Content classification

---

# Routing with Dapr workflow

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

return result;
```
