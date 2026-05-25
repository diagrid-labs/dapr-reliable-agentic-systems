---
layout: default
---

# Prompt Chaining

## When to Use

- Task can be decomposed into a fixed number of subtasks

## Use Cases

- Multi-step analysis
- Content generation pipeline
- Translation with refinement

---

# Prompt chaining with Dapr Workflow

```csharp
var step1 = await context.CallActivityAsync<string>(
    nameof(TranslateActivity), input);

var step2 = await context.CallActivityAsync<string>(
    nameof(RefineTranslationActivity), step1);

var step3 = await context.CallActivityAsync<Result>(
    nameof(EvaluateTranslationActivity), step2);

return step3;
```