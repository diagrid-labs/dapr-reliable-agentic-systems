---
layout: default
---

# Prompt Chaining

Sequential LLM calls where output of one becomes input to the next

## When to Use

- Task can be decomposed into fixed subtasks
- Trade latency for higher accuracy
- Each step needs focused attention

## Use Cases

- Multi-step analysis
- Content generation pipeline
- Translation with refinement

## Pros ✅

- Higher accuracy due to a focussed subtask
- Validation gates between steps

## Cons ❌

- Higher latency (sequential)
- Increased cost (multiple calls)