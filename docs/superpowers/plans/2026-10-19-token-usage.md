# Token Usage in Custom Status and LCARS UIs Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Every demo reports LLM token usage (prompt, completion, total) in the workflow custom status, and every LCARS UI shows the running total while the workflow runs and the final total after it completes.

**Architecture:** An LLM activity reads `response.Outputs.First().Usage` and returns it next to its normal output as `Metered<T>(Value, Usage)`. The workflow sums the usage of every completed activity and publishes the sum as a `Usage` field in its custom status (`SetCustomStatus`). The status endpoint already returns the custom status as `progress`, so the UI polls it as before; the shared `engine.js` renders `progress.usage` generically and the number counts up as polls arrive. Usage rides in activity output, so it is part of workflow history: replay-safe, and it survives the crash-and-resume demo.

**Tech Stack:** .NET 10, Dapr Workflow + `Dapr.AI` 1.18.10 (`ConversationResponseResult.Usage` is a `CompletionUsage`), vanilla JS/CSS.

**Spec:** none. This plan is derived from the token-usage investigation in this session (the OpenAI component fills `Usage` from langchaingo `GenerationInfo`, see `conversation/langchaingokit/translate.go` in dapr/components-contrib) and from the repo's existing patterns (`2026-10-18-aspire-demos-frontends-design.md`, `2026-10-18-lcars-animations-design.md`). Rulings made here are provisional.

## Global Constraints

- Solutions stay independent: the C# helper file is copied into each ApiService, nothing is shared between projects.
- Shared web files (`wwwroot/js/engine.js`, `wwwroot/js/util.js`, `wwwroot/css/lcars.css`, `appsettings.json`) stay **byte-identical** across the six `*.Web` projects (`md5 -q */*.Web/wwwroot/js/engine.js | sort -u | wc -l` → `1`, same for `util.js`, `lcars.css`). Per-demo files: `index.html`, `js/demo.js`, `css/demo.css`.
- Custom-status JSON is camelCase; the usage field is named `usage` in every demo: `{ "promptTokens": n, "completionTokens": n, "totalTokens": n }`.
- Animations use real data only (the count-up moves toward the last polled value, no fake progress), respect `prefers-reduced-motion`, and keep decorative elements `aria-hidden`; the live regions (`#run-status`, `#durable-caption`) are not touched.
- Usage may be `null` (a provider that reports none, or an OpenAI response without token info). Treat null as zero; the UI shows `--` until the first non-zero total.
- Use `Dapr.AI` types only; do not add packages. Code style per `AGENTS.md`: no comments except where logic is non-obvious, no new classes beyond the one helper file.
- Do not run `git add`/`git commit`/`git rm` (user stages and commits). Steps below say "checkpoint" where a commit would normally go: skip it.
- Never read or modify `Resources/conversation-openai.yaml`; it is not touched by this plan.

## Rulings (provisional)

- **Activity output carries usage** (`Metered<T>`) rather than a side channel (state store key per instance). A side channel needs an extra read activity per step (pollutes the Dev Dashboard history) and breaks replay purity. Cost: every LLM activity and its workflow call site changes (about 25 activities).
- **Failed attempts are not counted.** A retried activity only returns the usage of the attempt that succeeded; tokens spent by failed attempts are invisible. Documented in the READMEs, not fixed.
- **Total is shown, split is secondary.** The headline is `totalTokens`; prompt/completion appear as a small detail line. Reasoning/cached token details are ignored (gpt-4.1-mini reports none).
- **AnomalyAnalysis custom status changes from a bare list to an object** (`{ stages, usage }`). Cost: its `demo.js` readers change in the same task.
- **Usage published at the end of every LLM step**, plus one final publish after the last LLM activity so the completed instance's status holds the final total.

## Review Focus

- Provider returns `Usage == null` (Ollama, missing keys): status shows zeros, UI shows `--`, workflow and run still complete.
- Parallel fan-out (StarshipDiagnostics scans/votes, SpaceColonyPlanner workers, GalacticAnomalyClassifier analyzers): two activities finishing in one poll interval add up correctly; totals never go down between polls.
- Crash-and-resume (ApiService stopped mid-run): after replay the total equals the sum without double counting.
- Status with no `usage` yet (first poll before the first LLM step) and an older instance whose custom status lacks the field: UI shows `--`, no JS error.
- Totals above 1,000,000 and the count-up on a jump from 0 to a large value: formatted with grouping, finishes within the animation cap.
- Failed or terminated run: the last known total stays visible and stops moving.

---

## File Structure

| File | Responsibility |
|---|---|
| `<Name>.ApiService/Models/TokenUsage.cs` (new, 6 copies) | `TokenUsage` record (+ `Add`, `Zero`), `Metered<T>` record, `ToUsage()` extension on `ConversationResponse` |
| `<Name>.ApiService/Activities/**` | LLM activities return `Metered<T>` |
| `<Name>.ApiService/Workflows/*.cs`, `Models/*Progress*` | sum usage, publish `Usage` in the progress record |
| `<Name>.ApiService/Program.cs` | status endpoint reads the updated progress type |
| `<Name>.Web/wwwroot/js/engine.js`, `util.js`, `css/lcars.css` | generic readout (shared, identical) |
| `<Name>.Web/wwwroot/index.html` | readout elements in the progress and findings panels |
| `<Name>.ApiService/*.http`, `README.md`, `AGENTS.md` | docs |

---

### Task 1: AnomalyAnalysis backend (pilot)

**Files:**
- Create: `AnomalyAnalysis/AnomalyAnalysis.ApiService/Models/TokenUsage.cs`
- Modify: `AnomalyAnalysis.ApiService/Activities/{ProcessSensorData,ClassifyAnomaly,ScientificAnalysis,RiskAssessment,GenerateRecommendation}Activity.cs`, `Workflows/AnomalyAnalysisWorkflow.cs`, `Models/SpatialAnomaly.cs` (where `AnalysisStage` lives), `Program.cs:55`

**Interfaces:**
- Produces (namespace `AnomalyAnalysis.Models`, copied to the other five projects with their own namespace):
  - `public record TokenUsage(long PromptTokens, long CompletionTokens, long TotalTokens)` with `public static TokenUsage Zero`, `public static TokenUsage operator +(TokenUsage a, TokenUsage b)`.
  - `public record Metered<T>(T Value, TokenUsage Usage)`.
  - `public static TokenUsage ToUsage(this ConversationResponse response)`: sums `response.Outputs` usage, null-safe (null `Usage` → `Zero`). Verify the response type name and `CompletionUsage` property types against `~/.nuget/packages/dapr.ai/1.18.10/lib/net10.0/Dapr.AI.xml` before writing.
  - `public record AnalysisProgress(List<AnalysisStage> Stages, TokenUsage Usage)`: the new custom status.
- Activities `X : WorkflowActivity<In, string>` become `WorkflowActivity<In, Metered<string>>`; `AlertBridgeActivity` (no LLM) unchanged.

- [ ] **Step 1:** Add `TokenUsage.cs` as specified.
- [ ] **Step 2:** Change the five LLM activities to return `new Metered<string>(content, response.ToUsage())`.
- [ ] **Step 3:** In the workflow keep `var usage = TokenUsage.Zero;`, add each result's `Usage` after its call, unwrap `.Value` for the existing logic, and replace every `SetCustomStatus(stages)` by `SetCustomStatus(new AnalysisProgress(stages.ToList(), usage))`. The `AnalysisStage` entries and `AnalysisResult` stay as they are. Keep the gate checks working on `.Value`.
- [ ] **Step 4:** `Program.cs`: `progress = state.ReadCustomStatusAs<AnalysisProgress>()`.
- [ ] **Step 5: Verify build and a real run.** `dotnet build AnomalyAnalysis/AnomalyAnalysis.sln` → `Build succeeded.`, 0 errors. Run `aspire run` with the Ollama conversation component the earlier sessions used (or OpenAI if `OPENAI_API_KEY` is set), POST `start`, then GET `status` repeatedly. Expected: `progress.usage.totalTokens` is non-decreasing and the final value is greater than 0 for a provider that reports usage. If Ollama reports `null`, record that in the report, and prove the OpenAI path by logging `response.Outputs.First().Usage` once (remove the log afterwards). Also stop only the ApiService mid-run, restart, and confirm the final total equals prompt+completion sums without double counting.
- [ ] **Step 6:** Checkpoint (no git commands).

---

### Task 2: Shared readout (engine, util, css) piloted in AnomalyAnalysis

**Files:**
- Modify: `AnomalyAnalysis.Web/wwwroot/js/engine.js`, `js/util.js`, `css/lcars.css`, `wwwroot/index.html`, `js/demo.js` (read `progress.stages`; `checkpoints` uses `progress.stages.length`)

**Interfaces:**
- Consumes: custom status `progress` with optional `usage` (shape in Global Constraints).
- Produces (shared, identical in all six): optional ids `tokens-total`, `tokens-detail` in `#progress-panel` and `findings-tokens` in `#findings-panel`. If an id is absent the engine skips it. `util.js` exports `formatTokens(n: number): string` (grouped, e.g. `12,345`) and `countTo(el: Element, target: number, { ms = 600 }): void` (animates the displayed integer from its current value to `target`; instant when `!runState.animate`, when `reducedMotion()`, or when `target < current`).
- The engine calls an internal `renderUsage(progress, terminal)` from the poll path next to `renderDurable`, compare-then-write (identical polls mutate nothing, per the existing rule at the top of `engine.js`). The readout containers are `aria-hidden` while counting; the final total is set as plain text in `#findings-tokens` on completion so it is announced once with the findings.

- [ ] **Step 1: Write a fabricated-progress check first** (Procedure B from the animations plan): a throwaway page/script that feeds `engine.js` progress values `undefined`, `{}`, `{usage:null}`, `{usage:{totalTokens:0}}`, 120 → 480 → 3,200 → 1,234,567, and one polled value lower than the displayed one. Run it against the current engine to see the readout missing (RED).
- [ ] **Step 2:** Implement `formatTokens`, `countTo`, `renderUsage` and the CSS (LCARS-styled readout: a small pill with the label `Tokens`, the total in the display font, the detail line `prompt n · completion n` in the muted colour; no layout shift: reserve width with `font-variant-numeric: tabular-nums`).
- [ ] **Step 3:** Add the elements to AnomalyAnalysis `index.html` (progress panel header meta line; findings panel as a `dt/dd` pair `Token usage`), update `demo.js` for the `AnalysisProgress` shape (`progress?.stages`).
- [ ] **Step 4: Verify (Procedure B)**: the fabricated sequence counts up and never down, `--` for missing/zero, first render instant, reduced motion shows the value instantly, restart clears, Failed run keeps the last value and stops, MutationObserver on `#run-status`, `#durable-caption`, `#stage-list` shows no extra mutations, identical polls mutate nothing. Take and look at screenshots (running, completed). Then a real run from Task 1.
- [ ] **Step 5:** Copy `engine.js`, `util.js`, `lcars.css` with `cp` into the other five `*.Web` projects; run the md5 check → `1` distinct per file. The five UIs still work unchanged because every readout id is optional (spot check with one fabricated run).

---

### Task 3: AlienTranslator (iterative refinement loop)

**Files:** `AlienTranslator.ApiService/Models/TokenUsage.cs` (new), `Activities/{Translate,EvaluateTranslation,RefineTranslation}Activity.cs`, `Workflows/*`, `Models/Models.cs` (`TranslationProgress` gains `TokenUsage Usage`), `Program.cs` if the progress type changes, `AlienTranslator.Web/wwwroot/index.html`, `js/demo.js` if it needs the extra field (it should not).

**Interfaces:** Consumes Task 1's `TokenUsage`/`Metered<T>` (copy the file, change the namespace). `Translation` → `Metered<Translation>`, `Evaluation` → `Metered<Evaluation>`; `TranslationProgress(List<IterationProgress> Iterations, double Threshold, int MaxIterations, TokenUsage Usage)`.

- [ ] **Step 1:** Copy the helper; change the three activities; sum in the loop (add to `usage` after each of translate/evaluate/refine) and publish with the updated `TranslationProgress`; ensure a final publish after the last evaluation.
- [ ] **Step 2:** Add readout ids to `index.html` (same markup as Task 2).
- [ ] **Step 3: Verify:** `dotnet build AlienTranslator/AlienTranslator.sln`; real run: total grows each iteration, final value shown in the findings panel; screenshots of running/completed viewed.
- [ ] **Step 4:** Checkpoint (no git commands).

---

### Task 4: GalacticAnomalyClassifier (parallel analyzers + classify)

**Files:** `…ApiService/Models/TokenUsage.cs` (new), the five `Analyze*Activity.cs`, `ClassifyAnomalyActivity.cs`, `Workflows/*`, `Models/Models.cs` (`RoutingProgress` gains `TokenUsage Usage`), `Program.cs` if needed, `…Web/wwwroot/index.html`.

**Interfaces:** `Analyze*` outputs → `Metered<…Analysis>`, `ClassifyAnomalyActivity` → `Metered<AnomalyClassification>`; `RoutingProgress(string Stage, string? Type, double? Confidence, TokenUsage Usage)`. The routing path runs the classify call, then exactly one analyzer.

- [ ] **Step 1–3:** as Task 3 (helper, activities, workflow sum and publish at each stage including the last).
- [ ] **Step 4: Verify** with a run per anomaly type: total after Classifying is non-zero, increases again after Analyzing; screenshots viewed.
- [ ] **Step 5:** Checkpoint (no git commands).

---

### Task 5: SpaceColonyPlanner (fan-out workers)

**Files:** `…ApiService/Models/TokenUsage.cs` (new), `Activities/Analysis/{AnalyzePlanet,DetermineStructures,SynthesizePlan}Activity.cs`, `Activities/Workers/Plan*Activity.cs` (6 workers; `UnknownStructureActivity` and `OptimizeTimelineActivity` make no LLM call, confirm by grep), `Workflows/ColonyOrchestratorWorkflow.cs`, `Models/Models.cs` (`ColonyProgress` gains `TokenUsage Usage`), `…Web/wwwroot/index.html`.

**Interfaces:** `PlanXActivity` → `Metered<StructurePlan>`; `AnalyzePlanetActivity` → `Metered<PlanetAnalysis>`; `DetermineStructuresActivity` → `Metered<List<StructureRequest>>`; `SynthesizePlanActivity` → `Metered<ColonyMasterPlan>`; `ColonyProgress(string Stage, List<StructureProgress> Structures, TokenUsage Usage)`.

- [ ] **Step 1–3:** as Task 3. Workers finish in any order: add each worker's usage as it completes (the workflow already publishes per structure).
- [ ] **Step 4: Verify** with 0, 3 and 11 structures: totals monotonic, final total equals what the workflow output contributes; screenshots viewed.
- [ ] **Step 5:** Checkpoint (no git commands).

---

### Task 6: StarshipDiagnostics (parallel scans + voters)

**Files:** `…ApiService/Models/TokenUsage.cs` (new), `Activities/Scanners/*ScanActivity.cs` (5), `Activities/Voters/*VoterActivity.cs` (3), `AggregateResultsActivity` (check for LLM use), `Workflows/ParallelDiagnosticsWorkflow.cs`, `Models/Models.cs` (`DiagnosticsProgress` gains `TokenUsage Usage`), `…Web/wwwroot/index.html`.

**Interfaces:** `ScanResult` → `Metered<ScanResult>`; voters `KeyValuePair<string,string>` → `Metered<KeyValuePair<string,string>>` (verify the Dapr serializer round-trips it; if not, use a small record and rule on it in the ledger); `DiagnosticsProgress(string Stage, List<ScanProgress> Scans, List<VoteProgress> Votes, TokenUsage Usage)`.

- [ ] **Step 1–3:** as Task 3; add usage inside the existing `Task.WhenAny` loop (scans) and after the voters finish; `Task.WhenAll` results are unwrapped with `.Value` for aggregation.
- [ ] **Step 4: Verify:** parallel scans arriving in one poll add up, totals never decrease; screenshots viewed.
- [ ] **Step 5:** Checkpoint (no git commands).

---

### Task 7: SpaceDebrisAgent (agent loop with approval)

**Files:** `…ApiService/Models/TokenUsage.cs` (new), `Activities/Agent/AgentReasoningActivity.cs`, `Activities/GenerateReportActivity.cs` (the LLM callers; the tool activities make no LLM calls, confirm by grep), `Workflows/SpaceDebrisCleanupWorkflow.cs`, `Models/Models.cs` (`MissionProgress` gains `TokenUsage Usage`, keep the existing positional order and append), `Program.cs:116` (keeps working since the type is the same record), `…Web/wwwroot/index.html`.

**Interfaces:** `AgentReasoningActivity` → `WorkflowActivity<ReasoningInput, Metered<AgentDecision>>`; `GenerateReportActivity` → `Metered<string>`. The four `SetCustomStatus(new MissionProgress(...))` calls (`SpaceDebrisCleanupWorkflow.cs:44,56,89,107`) all pass the running `usage`.

- [ ] **Step 1–3:** as Task 3. The approval wait (`WaitForExternalEvent`) must not reset or lose `usage` when the status is republished (`usage` lives in a workflow local, so it survives).
- [ ] **Step 4: Verify** a mission with approval and a mission without: total grows per reasoning step, stays flat while waiting for approval, final total includes the report; the approval-panel check script from the animations work (`debris-demo.mjs`, 59 checks) still passes; screenshots viewed.
- [ ] **Step 5:** Checkpoint (no git commands).

---

### Task 8: Docs and final checks

**Files:** each `<Name>/README.md` (a short "Token usage" paragraph: where it comes from, null/zero case, failed attempts not counted), each `*.ApiService.http` (no new endpoints, so only a note if the status example shows a `progress` sample), `AGENTS.md` (front-end rules: readout ids are optional and the shared files list is unchanged; back-end rules: LLM activities return `Metered<T>`).

- [ ] **Step 1:** Update the docs as listed; keep the READMEs' existing tone.
- [ ] **Step 2: Final checks:** `for p in AlienTranslator AnomalyAnalysis GalacticAnomalyClassifier SpaceColonyPlanner SpaceDebrisAgent StarshipDiagnostics; do dotnet build $p/$p.sln; done` → `Build succeeded.` x6; md5 distinct count 1 for `engine.js`, `util.js`, `lcars.css`, `appsettings.json`; `grep -rn "ConverseAsync" --include='*.cs' */*.ApiService/Activities | grep -v ToUsage` reports no activity that drops usage; no `bin/obj` untracked; no running processes left; `Resources/` folders untouched.
- [ ] **Step 3:** One full run per demo with a usage-reporting provider and a screenshot of the completed findings panel showing the total (looked at, not just taken).

## Notes

- **Tests:** the repo has no test projects; each task ends in a build plus real or fabricated browser checks with expected results, as in the earlier plans.
- **Caveat to verify in Task 1:** whether Ollama populates usage. If it does not, all real verification of non-zero totals needs `OPENAI_API_KEY`.
