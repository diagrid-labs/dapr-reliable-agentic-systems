---
layout: default
---

## Orchestrator-Workers

### Pattern Overview

Central orchestrator dynamically decomposes tasks and delegates to workers

### When to Use

- Subtasks can't be predicted upfront
- Dynamic decomposition needed
- Complex, unpredictable task structures

### Pros ✅

- Flexible and adaptable
- Scales to complex problems
- Clear separation of concerns

### Cons ❌

- Higher complexity
- More LLM calls (orchestrator + workers)
- Higher latency and cost
