---
layout: section
customTheme: .demo/slides/theme/theme.css
---

# Prompt Chaining

Sequential LLM calls where output of one becomes input to the next

---
layout: default
customTheme: .demo/slides/theme/theme.css
---

# Prompt Chaining

## When to Use

- Task can be decomposed into fixed subtasks
- Trade latency for higher accuracy
- Each step needs focused attention

## Use Cases

- Multi-step analysis
- Content generation pipeline
- Translation with refinement

---
layout: default
customTheme: .demo/slides/theme/theme.css
---

# Prompt Chaining

## Pros ✅

- Easy to understand and implement
- High accuracy due to focussed subtasks
- Validation gates between tasks

## Cons ❌

- Higher latency (sequential)
- Increased cost (multiple calls)

---
layout: default
customTheme: .demo/slides/theme/theme.css
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