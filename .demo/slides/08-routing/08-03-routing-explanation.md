---
layout: default
customTheme: .demo/slides/theme/theme.css
---

# Routing

## When to Use

- Distinct categories better handled separately
- Classification can be accurate
- Separation of concerns needed

## Use Cases

- Customer service routing
- Content classification
- Model size optimization

---

# Routing

## Pros ✅

- Specialized prompts per category
- Cost optimization (route to appropriate model)

## Cons ❌

- Classification adds latency
- Classification accuracy depends on routing decisions

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
