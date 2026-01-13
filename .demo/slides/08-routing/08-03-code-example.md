---
layout: default
---

# Dapr workflow example

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
