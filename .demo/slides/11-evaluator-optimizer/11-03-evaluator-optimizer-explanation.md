---
layout: default
customTheme: .demo/slides/theme/theme.css
---

# Evaluator-Optimizer

## When to Use

- Clear evaluation criteria exist
- Iterative refinement adds value
- LLM can provide useful feedback
- Quality more important than speed

## Use Cases

- Literary translation
- Content quality improvement
- Code generation with validation

---

# Evaluator-Optimizer

## Pros ✅

- Progressive quality improvement
- Built-in quality control
- Mimics human refinement process

## Cons ❌

- Unpredictable iterations
- High cost and latency
- Risk of infinite loops

---

# Evaluator-Optimizer with Dapr workflow

```csharp
double threshold = 0.8;
int maxIterations = 5;

var current = await context.CallActivityAsync<string>(
    nameof(GenerateActivity), input);

var quality = await context.CallActivityAsync<double>(
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
