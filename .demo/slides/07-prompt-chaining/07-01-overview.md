---
layout: default
---

## Prompt Chaining

### Pattern Overview

Sequential LLM calls where output of one becomes input to the next

### When to Use

- Task can be decomposed into fixed subtasks
- Trade latency for higher accuracy
- Each step needs focused attention

### Pros ✅

- Higher accuracy through decomposition
- Validation gates between steps
- Clear audit trail

### Cons ❌

- Higher latency (sequential)
- Increased cost (multiple calls)
- Fixed path (not flexible)
