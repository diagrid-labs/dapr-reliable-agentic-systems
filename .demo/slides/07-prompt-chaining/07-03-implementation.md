---
layout: default
---

## Implementation with Dapr

### Code Example

```csharp
var step1 = await context.CallActivityAsync<string>(
    nameof(TranslateActivity), input);

var step2 = await context.CallActivityAsync<string>(
    nameof(RefineTranslationActivity), step1);

var step3 = await context.CallActivityAsync<Result>(
    nameof(EvaluateTranslationActivity), step2);
```

### Benefits with Workflow

- Each step automatically checkpointed
- Restart from failure point
- No re-execution of expensive LLM calls
