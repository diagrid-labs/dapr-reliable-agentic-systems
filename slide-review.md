# DemoTime Slide Review Report

## Summary

This report identifies inconsistencies between DemoTime YAML files in `.demo/` and their corresponding slide markdown files in `.demo/slides/`.

---

## 01-introduction.yaml

**Status:** ✅ All slides exist

- ✅ `.demo/slides/01-intro/01-01-title.md`
- ✅ `.demo/slides/01-intro/01-02-problem.md`

---

## 02-agentic-systems-and-issues.yaml

**Status:** ❌ 1 missing file

- ✅ `.demo/slides/02-agentic-systems/02-01-what-are-agents.md`
- ❌ `.demo/slides/02-agentic-systems/02-02-reality.md` (Referenced in YAML but missing)
  - **Actual file:** `02-02-everyone-is-building-agents.md`
- ✅ `.demo/slides/02-agentic-systems/02-03-challenges.md`
- ✅ `.demo/slides/02-agentic-systems/02-04-distributed-systems.md`

---

## 03-durable-execution.yaml

**Status:** ✅ All slides exist

- ✅ `.demo/slides/03-durable-execution/03-01-what-is-durable-execution.md`
- ✅ `.demo/slides/03-durable-execution/03-02-benefits.md`

---

## 04-dapr-workflow.yaml

**Status:** ✅ All slides exist

- ✅ `.demo/slides/04-dapr-workflow/04-01-what-is-dapr.md`
- ✅ `.demo/slides/04-dapr-workflow/04-02-workflow-building-block.md`
- ✅ `.demo/slides/04-dapr-workflow/04-03-programming-model.md`
- ✅ `.demo/slides/04-dapr-workflow/04-04-workflow.md`
- ✅ `.demo/slides/04-dapr-workflow/04-05-activity.md`
- ✅ `.demo/slides/04-dapr-workflow/04-06-workflow-management.md`

---

## 05-conversation-api.yaml

**Status:** ✅ All slides exist

- ✅ `.demo/slides/05-conversation-api/05-01-introducing.md`
- ✅ `.demo/slides/05-conversation-api/05-02-code-example.md`
- ✅ `.demo/slides/05-conversation-api/05-03-component-example.md`

---

## 06-agentic-patterns-overview.yaml

**Status:** ❌ 3 issues (wrong folder + missing files)

**Wrong folder:** References `06-patterns-overview` but actual folder is `06-combining-workflow-llm`

- ❌ `.demo/slides/06-patterns-overview/06-01-patterns-list.md` (Referenced in YAML but wrong path)
  - **Actual folder:** `06-combining-workflow-llm`
  - **Actual files in folder:**
    - `06-01-combining.md`
    - `06-02-patterns-list.md`
    - `06-03-selection-guide.md`
- ❌ `.demo/slides/06-patterns-overview/06-02-selection-guide.md` (Referenced in YAML but wrong path)
  - **Should be:** `.demo/slides/06-combining-workflow-llm/06-03-selection-guide.md`

**Missing in YAML:**

- `06-01-combining.md` (exists in folder but not referenced in YAML)

---

## 07-prompt-chaining.yaml

**Status:** ❌ 1 missing file

- ✅ `.demo/slides/07-prompt-chaining/07-01-overview.md`
- ✅ `.demo/slides/07-prompt-chaining/07-02-diagram.md`
- ❌ `.demo/slides/07-prompt-chaining/07-03-implementation.md` (Referenced in YAML but missing)
  - **Actual file:** `07-03-code-example.md`
- ✅ `.demo/slides/07-prompt-chaining/07-04-demo.md`

**Missing in YAML:**

- `07-05-demo-diagram.md` (exists in folder but not referenced in YAML)

---

## 08-routing.yaml

**Status:** ❌ 1 missing file

- ✅ `.demo/slides/08-routing/08-01-overview.md`
- ✅ `.demo/slides/08-routing/08-02-diagram.md`
- ❌ `.demo/slides/08-routing/08-03-implementation.md` (Referenced in YAML but missing)
  - **Actual file:** `08-03-code-example.md`
- ✅ `.demo/slides/08-routing/08-04-demo.md`

**Missing in YAML:**

- `08-05-demo-diagram.md` (exists in folder but not referenced in YAML)

---

## 09-parallelization.yaml

**Status:** ❌ 1 missing file

- ✅ `.demo/slides/09-parallelization/09-01-overview.md`
- ✅ `.demo/slides/09-parallelization/09-02-diagrams.md`
- ❌ `.demo/slides/09-parallelization/09-03-demo.md` (Referenced in YAML but missing)
  - **Actual file:** `09-03-code-example.md`

**Missing in YAML:**

- `09-04-demo.md` (exists in folder but not referenced in YAML)
- `09-05-demo-diagram.md` (exists in folder but not referenced in YAML)

---

## 10-orchestrator-workers.yaml

**Status:** ❌ 1 missing file

- ✅ `.demo/slides/10-orchestrator-workers/10-01-overview.md`
- ✅ `.demo/slides/10-orchestrator-workers/10-02-diagram.md`
- ❌ `.demo/slides/10-orchestrator-workers/10-03-implementation.md` (Referenced in YAML but missing)
  - **Actual file:** `10-03-code-example.md`
- ✅ `.demo/slides/10-orchestrator-workers/10-04-demo.md`

**Missing in YAML:**

- `10-05-demo-diagram.md` (exists in folder but not referenced in YAML)

---

## 11-evaluator-optimizer.yaml

**Status:** ❌ 1 missing file

- ✅ `.demo/slides/11-evaluator-optimizer/11-01-overview.md`
- ✅ `.demo/slides/11-evaluator-optimizer/11-02-diagram.md`
- ❌ `.demo/slides/11-evaluator-optimizer/11-03-implementation.md` (Referenced in YAML but missing)
  - **Actual file:** `11-03-code-example.md`
- ✅ `.demo/slides/11-evaluator-optimizer/11-04-demo.md`

**Missing in YAML:**

- `11-05-demo-diagram.md` (exists in folder but not referenced in YAML)

---

## 12-autonomous-agent.yaml

**Status:** ❌ 1 missing file

- ✅ `.demo/slides/12-autonomous-agent/12-01-overview.md`
- ✅ `.demo/slides/12-autonomous-agent/12-02-diagram.md`
- ❌ `.demo/slides/12-autonomous-agent/12-03-implementation.md` (Referenced in YAML but missing)
  - **Actual file:** `12-03-code-example.md`
- ✅ `.demo/slides/12-autonomous-agent/12-04-demo.md`

**Missing in YAML:**

- `12-05-demo-diagram.md` (exists in folder but not referenced in YAML)

---

## 13-summary.yaml

**Status:** ✅ All slides exist

- ✅ `.demo/slides/13-summary/13-01-key-takeaways.md`
- ✅ `.demo/slides/13-summary/13-02-pattern-summary.md`
- ✅ `.demo/slides/13-summary/13-03-getting-started.md`
- ✅ `.demo/slides/13-summary/13-04-questions.md`

---

## Overall Statistics

- **Total YAML files:** 11
- **YAML files with issues:** 9
- **Total missing/misnamed slides:** 12
- **Total unreferenced slides:** 6

## Common Patterns

1. **"implementation" vs "code-example":** Files 07-12 all reference `*-03-implementation.md` in YAML but actual files are named `*-03-code-example.md`
2. **Missing demo diagram references:** Files 07-08, 10-12 have `*-05-demo-diagram.md` files that are not referenced in YAML
3. **File 09 has additional unreferenced files:** `09-04-demo.md` and `09-05-demo-diagram.md`

## Recommendations

1. **Update YAML references** to match actual file names
2. **Add missing slide references** to YAML files for unreferenced slides
3. **Fix folder path** in `06-agentic-patterns-overview.yaml` from `06-patterns-overview` to `06-combining-workflow-llm`
4. **Add missing slide** `06-01-combining.md` reference to `06-agentic-patterns-overview.yaml`
5. **Update YAML reference** from `02-02-reality.md` to `02-02-everyone-is-building-agents.md`.
