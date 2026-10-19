# LCARS Front-ends for the Remaining Aspire Demos Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a Star Trek TNG (LCARS) web UI project to GalacticAnomalyClassifier, StarshipDiagnostics, SpaceColonyPlanner, AlienTranslator and SpaceDebrisAgent that starts the workflow, shows its progress and shows the output, reusing the AnomalyAnalysis implementation.

**Architecture:** Each demo gets its own `<Name>.Web` project (static vanilla JS + YARP proxy to `wf-app`), copied from `AnomalyAnalysis.Web` and then owned by that demo. The front-end is a generic `engine.js` plus a per-demo `demo.js` config. Each ApiService publishes progress through `SetCustomStatus` and returns it as `progress` in a uniform status response.

**Tech Stack:** .NET 10, Aspire 13.6.1, Dapr.Workflow 1.18.10, YARP (`Yarp.ReverseProxy` 2.3.0, `Microsoft.Extensions.ServiceDiscovery.Yarp` 10.10.0), vanilla HTML/CSS/JS (ES modules), Antonio font (self-hosted).

**Spec:** `docs/superpowers/specs/2026-10-18-aspire-demos-frontends-design.md` (read it with this plan; per-demo payloads, layouts and result models are specified there). Reference implementation: `AnomalyAnalysis/AnomalyAnalysis.Web` and `docs/superpowers/specs/2026-10-18-anomaly-analysis-frontend-design.md`.

## Global Constraints

- Vanilla HTML/CSS/JS only: no frameworks, no Node toolchain, no bundler, no chart library (`AGENTS.md`).
- **Solutions stay independent:** no shared project, linked folder or shared package; duplication across the Aspire projects is intended. Nothing is diffed or kept in sync between demos.
- Web project: `Microsoft.NET.Sdk.Web`, net10.0, fixed port `5501`, calls `builder.AddServiceDefaults()` first and `app.MapDefaultEndpoints()` before `app.Run()`; AppHost resource name `web`; ApiService resource stays `wf-app` on `http://localhost:5500`.
- Polling, not SSE: a `setTimeout` chain every 2 s (5 s after errors); give up after 30 s of consecutive 404s.
- Render server text only with `textContent`; strip `**` and leading `#` markers via `clean()`; never `innerHTML`.
- Status endpoints: `if (!state.Exists) return Results.NotFound();` read the output only when `state.RuntimeStatus == WorkflowRuntimeStatus.Completed`; return the uniform shape `{ instanceId, status, result, progress, createdAt, lastUpdatedAt }`.
- No Star Trek logos, images or audio files; CSS only; Antonio self-hosted; honour `prefers-reduced-motion`; live regions for status text; move focus to the active panel heading on view changes.
- Code style per `AGENTS.md`: minimal API, small modular code, comments only where logic is complex. `.gitattributes` `eol=lf`.
- Do not run `git add`, `git commit` or `git rm`; the user stages and commits.
- Wait about 10 s between `aspire stop` and the next `aspire run` (Valkey container name clash).

## Procedures used by several tasks

**Procedure A: local LLM for verification.** Without `OPENAI_API_KEY` the sidecar exits at startup. For live checks only: move `<Name>.AppHost/Resources/conversation-openai.yaml` into the session scratchpad, create `Resources/conversation.yaml` (`conversation.ollama`, component name `conversation`, model `glm-5.3-flash:cloud`), run `aspire run`, wait until `wf-app-dapr-cli` is Running/Healthy plus about 15 s. When finished: `aspire stop`, delete the temporary `conversation.yaml`, move `conversation-openai.yaml` back, and confirm `git status` shows no change under `Resources/`.

**Procedure B: browser verification.** `puppeteer-core` is installed in the session scratchpad (`<scratchpad>/e2e`); drive `/Applications/Google Chrome.app` headless at 1280x900. Scripts live in the scratchpad, never in the repo. Each demo task names the checks. The generic checks every demo must pass (written once as `<scratchpad>/e2e/common.mjs` in Task 1 and reused): page loads with no console errors, no requests leave localhost, offline blip shows `#link-lost` and recovers, double click on `#begin` creates exactly one POST, a terminated instance (`dapr workflow terminate -a <app-id> --output '"x"' <id>`) reaches the failed view with `#restart` visible, a 404-only status ends in the failed view after about 30 s, `#restart` returns to idle with focus on the form heading, HTML in a result field renders as text.

## Review Focus

- Unknown instance id: status endpoint returns 404 (not 200 `Unknown`), the UI ends in the failed view with a way out (every demo).
- Terminated run with a string output: status endpoint returns 200 with `result: null`, UI shows the failed view (every demo).
- Parallel fan-out progress (Starship, Colony): tiles/lanes complete in any order; the result of the run is identical to before the `WhenAny` change.
- Progress across `ContinueAsNew` (Alien, Debris): the progress never flashes empty and iterations/decisions never disappear after the loop restarts.
- Approval panel (Debris): double click sends one POST, a failed POST re-enables the buttons, the panel closes when the workflow stops waiting (decision or the 5-minute timeout), and a stale panel never survives into the next run.
- LLM free text in labels (threat level, overall status, risk): unknown wording falls back to showing the text, never throws; dynamic key/value rows (Galactic measurements, Alien vocabulary) reject empty or non-numeric entries with a form error.

---

## File Structure (per demo `<Name>`; AnomalyAnalysis is the reference)

| File | Responsibility |
|------|----------------|
| `<Name>.ApiService/Program.cs` | Status endpoint fixes and uniform shape |
| `<Name>.ApiService/Workflows/*.cs` | `SetCustomStatus` progress publishing |
| `<Name>.Web/<Name>.Web.csproj`, `Program.cs`, `appsettings.json`, `Properties/launchSettings.json` | Static files + YARP proxy (copied from the reference, names changed) |
| `<Name>.Web/wwwroot/index.html` | Per-demo form, panels and labels |
| `<Name>.Web/wwwroot/css/lcars.css` | LCARS look (copied) |
| `<Name>.Web/wwwroot/css/demo.css` | Per-demo styles only |
| `<Name>.Web/wwwroot/js/util.js` | `$`, `clean`, `levelFrom` (copied; no imports, so no import cycle) |
| `<Name>.Web/wwwroot/js/engine.js` | Generic run lifecycle (copied) |
| `<Name>.Web/wwwroot/js/demo.js` | Per-demo config: build payload, presets, progress and result rendering |
| `<Name>.AppHost/AppHost.cs`, `.csproj`, `<Name>.sln` | `web` resource and project reference |
| `<Name>/README.md` | "Use the web UI" section |

---

### Task 0: Engine/demo split in AnomalyAnalysis (reference gate)

**Files:**
- Create: `AnomalyAnalysis/AnomalyAnalysis.Web/wwwroot/js/util.js`, `js/engine.js`, `js/demo.js`
- Delete: `AnomalyAnalysis/AnomalyAnalysis.Web/wwwroot/js/app.js` (replaced; plain file delete, no `git rm`)
- Modify: `wwwroot/index.html` (script tag, form id `demo-form`, `<link>` unchanged)

**Interfaces:**
- Produces `util.js`: `$(id: string): HTMLElement`, `clean(text: unknown): string` (strip `**` and leading `#`, trim), `levelFrom(text: string, levels: string[], label?: RegExp): string | null` (case-insensitive: first match of `label` capture constrained to `levels`, else first level word in `text`; returns upper-case or `null`).
- Produces `demo.js` default export (the **config contract** every demo implements):
  ```js
  { start: { path: string, build(): object },        // build throws Error(message) on invalid input
    statusPath(instanceId: string): string,
    presets: Record<string, () => void>,             // keyed by button data-preset
    initialPreset: string,
    stardate?(): string | null,                      // optional header value, refreshed on form input
    renderProgress(status: string, progress: unknown | null): void,
    renderResult(result: object): { alert: boolean, note?: string },
    onPolled?(state: object): void,                  // optional, every successful poll
    resetUi?(): void }                               // optional, when a run is reset
  ```
- Produces `engine.js` (imports `./demo.js` default and `./util.js`; exports nothing) with the DOM contract: `#demo-form`, `#begin`, `#form-error`, `#alert-banner`, `#link-lost`, `#link-indicator`, `#run-status`, `#instance-id`, `#elapsed`, `#run-note`, `#restart`, `#new-analysis`, `#findings-note`, `#stardate`, panels `#form-panel`, `#progress-panel`, `#findings-panel`, `button[data-preset]`. Engine behaviour: unchanged from the current `app.js` (state machine `idle|running|completed|failed`, run token cancellation, 2 s/5 s polling, 404 give-up at 15 consecutive, elapsed clock, focus on `VIEW_HEADING`, `body.red-alert` from `renderResult().alert`, keeps the last non-null `progress`). It also keeps exposing `window.renderResult` for manual checks.

- [ ] **Step 1: Capture the baseline.** With Procedure A running, run the existing scripted flows from the AnomalyAnalysis verification (full run, link lost, terminate, 404, XSS, restart, focus) against the current `app.js` and note the outputs.
- [ ] **Step 2: Create `util.js` and `engine.js`** by moving the generic functions out of `app.js` (everything except `PRESETS`, `buildAnomaly`, `riskLabel`, `renderResult`, the stage list handling in `renderProgress`, and `updateStardate`).
- [ ] **Step 3: Create `demo.js`** holding the anomaly-specific code behind the config contract above; `renderResult` returns `{ alert: label === "CRITICAL", note }` using `levelFrom`.
- [ ] **Step 4: Update `index.html`** (form id `demo-form`, `<script type="module" src="js/engine.js">`), delete `app.js`.
- [ ] **Step 5: Verify no regression.** Run: `dotnet build AnomalyAnalysis/AnomalyAnalysis.sln` → `Build succeeded.`; then the baseline flows again. Expected: identical results to Step 1 (stages go active→done, completed view, Red Alert only for label CRITICAL, link-lost recovery, terminate → failed view with `#restart`, 404 give-up, restart focuses the form heading, XSS stays text). Any difference: fix before moving on.
- [ ] **Step 6: Write `<scratchpad>/e2e/common.mjs`** implementing the generic checks of Procedure B as an exported `runCommon({ baseUrl, appId, startBy })` function and run it against AnomalyAnalysis; expected: all PASS.

---

### Task 1: GalacticAnomalyClassifier — ApiService

**Files:**
- Modify: `GalacticAnomalyClassifier/GalacticAnomalyClassifier.ApiService/Program.cs`, `Workflows/AnomalyRoutingWorkflow.cs`

**Interfaces:**
- Produces: `GET /anomaly/status/{id}` → uniform shape; `progress` = `{ stage: "Classifying" | "Analyzing", type?: string, confidence?: number }`; `result.type` serialized as a string (`"TemporalRift"`).

- [ ] **Step 1: Status endpoint.** Replace `state == null` by `!state.Exists`, read `result` only when `Completed`, add `progress = state.ReadCustomStatusAs<JsonElement?>()` (or a small record `RoutingProgress(string Stage, string? Type, double? Confidence)`), keep `createdAt`/`lastUpdatedAt`.
- [ ] **Step 2: Enum converter.** In `ConfigureHttpJsonOptions` add `JsonStringEnumConverter` (as Alien/Colony/Debris do), plus case-insensitive property names if not present.
- [ ] **Step 3: Progress.** In `AnomalyRoutingWorkflow.RunAsync` call `context.SetCustomStatus(new RoutingProgress("Classifying", null, null))` first and `new RoutingProgress("Analyzing", anomalyType.ToString(), classification.Confidence)` right after `ParseAnomalyType`.
- [ ] **Step 4: Build and verify live.** Run `dotnet build GalacticAnomalyClassifier/GalacticAnomalyClassifier.sln` → `Build succeeded.`; with Procedure A: `curl localhost:5500/anomaly/status/none` → `404`; start the temporal-rift payload from the `.http` file and poll `status`.
  Expected: `status` `Running` with `progress.stage` `Classifying`, then `Analyzing` with `progress.type` `TemporalRift`, then `Completed` with `result.type` equal to the string `"TemporalRift"`. Terminate a second run with `--output '"x"'`: the endpoint returns 200 with `result: null`.

---

### Task 2: GalacticAnomalyClassifier — Web project, AppHost, README

**Files:**
- Create: `GalacticAnomalyClassifier/GalacticAnomalyClassifier.Web/**` (copy `AnomalyAnalysis.Web`, rename project/namespace strings, keep ports and proxy config)
- Modify: `GalacticAnomalyClassifier.AppHost/AppHost.cs` and `.csproj`, `GalacticAnomalyClassifier.sln`, `GalacticAnomalyClassifier/README.md`

**Interfaces:**
- Consumes: Task 0 config contract and DOM ids; Task 1 `progress` shape; payload `SpaceAnomaly` (`anomalyId`, `sensorData`, `coordinates`, `detectedAt`, `measurements: { string: number }`).
- Produces: web UI at `http://localhost:5501`.

- [ ] **Step 1: Copy and rename** the AnomalyAnalysis Web project (csproj name and `RootNamespace`, `AppHost` reference); keep `util.js`, `engine.js`, `lcars.css`, font unchanged.
- [ ] **Step 2: `index.html`.** Title "Anomaly Classifier", sidebar labels SENSOR DATA / ROUTING / ANALYSIS. Form: `#sensorData` (textarea), `#coordinates`, `#detectedAt`, a measurements list (`#measurements` with rows of name + number inputs, `#add-measurement` button; at least one row required). Preset buttons `data-preset` = `temporal-rift`, `dark-matter`, `alien-artifact`, `stellar-phenomenon`, `dimensional-tear` with the five payloads from the `.http` file. Progress panel: `#classifier-node` and a lane per type `li[data-type="TemporalRift|DarkMatterCluster|AlienArtifact|StellarPhenomenon|DimensionalTear"]`. Findings panel: `#anomaly-type`, `#threat-level`, `#classification`, `#specialized-analysis`, a `#metrics` table body, `#recommendations` list.
- [ ] **Step 3: `demo.js`.** `start.path` `/api/anomaly/analyze`, `build()` returns `SpaceAnomaly` with `anomalyId: "CLASS-" + crypto.randomUUID()` (rejects empty/non-numeric measurement rows with an Error), `statusPath` `/api/anomaly/status/<id>`. `renderProgress(status, progress)`: `Classifying` or null → classifier pulsing, lanes dim; `Analyzing` with `type` → that lane active, others `NOT ROUTED`; unknown/`Unknown` type → no lane lit and a note. `renderResult(result)`: fill the panel (metrics values rounded to 4 significant digits, via `textContent`); `alert` when `levelFrom(result.threatLevel, ["CRITICAL","SEVERE"])` is non-null; `note` for type `Unknown`.
- [ ] **Step 4: `demo.css`** for lanes, classifier node and the measurements rows only.
- [ ] **Step 5: Wire AppHost** with the `web` project resource (`WithReference(workflowApp)`, `WaitFor(workflowApp)`, `WithExternalHttpEndpoints()`), `ProjectReference` in the csproj, add the project to the `.sln` (`dotnet sln ... add`).
- [ ] **Step 6: README.** Add the `web` resource to the `aspire run` list and a "Use the web UI" section (presets, routing board, result view).
- [ ] **Step 7: Verify.** `dotnet build GalacticAnomalyClassifier/GalacticAnomalyClassifier.sln` → `Build succeeded.`; with Procedure A run `common.mjs` plus the demo checks (Procedure B): the first preset shows the classifier completing and exactly one lane active for its type; the dark-matter preset routes to another lane; a result with type `Unknown` (call `window.renderResult` with a fabricated result) shows the note; a measurement row with `abc` blocks the start with a form error.

---

### Task 3: StarshipDiagnostics — ApiService

**Files:**
- Modify: `StarshipDiagnostics/StarshipDiagnostics.ApiService/Program.cs`, `Workflows/ParallelDiagnosticsWorkflow.cs`

**Interfaces:**
- Produces: `GET /ship/report/{id}` → uniform shape; `progress` = `{ stage: "Scanning"|"Voting"|"Aggregating", scans: [{ activity: string, subsystem: string, status: string?, healthPercentage: number }], votes: [{ category: string, consensus: string, agreementLevel: number }] }` (record `DiagnosticsProgress`).
- `activity` values are the five `nameof(...ScanActivity)` strings: `HullIntegrityScanActivity`, `ReactorCoreScanActivity`, `NavigationScanActivity`, `LifeSupportScanActivity`, `WeaponsScanActivity`.

- [ ] **Step 1: Status endpoint** as in Task 1 Step 1 (`ReadOutputAs<DiagnosticReport>()` only when `Completed`, plus `progress`).
- [ ] **Step 2: Scan progress.** Keep the scan task list but pair each task with its activity name; replace `Task.WhenAll(scanTasks)` by a loop that `await Task.WhenAny(pending)`, removes the finished task, appends its `ScanResult` to the progress and calls `context.SetCustomStatus(progress)`; the final `scanResults` list must keep the original order (index by task position) so `AggregateResultsActivity` input is unchanged. Publish `stage: "Scanning"` before the loop.
- [ ] **Step 3: Vote progress.** Set `stage: "Voting"` before the voting block; after each finding's `Task.WhenAll(voteTasks)` append the `VoteResult` summary to `votes` and republish; set `stage: "Aggregating"` before `AggregateResultsActivity`.
- [ ] **Step 4: Build and verify live.** `dotnet build StarshipDiagnostics/StarshipDiagnostics.sln` → `Build succeeded.`; with Procedure A start the `NCC-1701` payload from the `.http` file and poll.
  Expected: `progress.scans` grows 0→5 entries, in any order, then (for the aging ship) `stage` `Voting` with `votes` filling, then `Completed`; `result` still matches the shape of `DiagnosticReport` and its `subsystemScans` order is Hull, Reactor, Navigation, Life support, Weapons. Unknown id → 404; terminate with `--output '"x"'` → 200, `result: null`.

---

### Task 4: StarshipDiagnostics — Web project, AppHost, README

**Files:**
- Create: `StarshipDiagnostics/StarshipDiagnostics.Web/**` (copy as in Task 2 Step 1)
- Modify: AppHost `.cs`/`.csproj`, `.sln`, `StarshipDiagnostics/README.md`

**Interfaces:**
- Consumes: Task 0 contract; Task 3 `DiagnosticsProgress`; payload `Starship` (`shipId`, `name`, `class`, `yearsInService`, `lastMaintenance`, `telemetry` with `hullStressLevel`, `reactorTemperature`, `shieldIntegrity`, `oxygenLevel`, `navigationAccuracy`, `weaponsPower`); result `DiagnosticReport`.

- [ ] **Step 1: Copy and rename** (as Task 2 Step 1).
- [ ] **Step 2: `index.html`.** Title "Diagnostic Console", sidebar SHIP DATA / SCANS / REPORT. Form: `#shipId`, `#name`, `#class`, `#yearsInService`, `#lastMaintenance`, six telemetry inputs (`input[type=range]` with numeric readout for the 0-1 fields; number input for `reactorTemperature`). Presets `enterprise-d`, `enterprise-original`, `enterprise-e` (values from the `.http` file). Progress panel: five tiles `[data-activity="<ActivityName>"]` and `#vote-panel` (rows added dynamically). Findings: `#departure` indicator, `#overall-status`, subsystem rows container, `#votes`, `#repairs` list, `#repair-hours`.
- [ ] **Step 3: `demo.js`.** `start.path` `/api/ship/diagnose`, `statusPath` `/api/ship/report/<id>`. `renderProgress`: tile state from `progress.scans[].activity` (SCANNING until present, then health % and status colour class `ok|warning|critical` from `status` via `levelFrom`), vote panel rows from `votes` plus pending placeholders for critical scans when `stage` is `Voting`. `renderResult`: `#departure` text `CLEAR FOR DEPARTURE` / `DEPARTURE HELD` from `clearForDeparture`; `alert: !result.clearForDeparture`.
- [ ] **Step 4: `demo.css`**, **Step 5: AppHost/sln**, **Step 6: README** as in Task 2.
- [ ] **Step 7: Verify.** Build → `Build succeeded.`; Procedure A + `common.mjs` + demo checks: the `enterprise-e` preset (healthy) completes with `CLEAR FOR DEPARTURE` and no alert; the `enterprise-original` preset shows tiles completing (observe at least one tile `COMPLETE` while another is `SCANNING`), a vote panel for critical findings, and `DEPARTURE HELD` with Red Alert if `clearForDeparture` is false; slider values reach the POST body as numbers.

---

### Task 5: SpaceColonyPlanner — ApiService

**Files:**
- Modify: `SpaceColonyPlanner/SpaceColonyPlanner.ApiService/Program.cs`, `Workflows/ColonyOrchestratorWorkflow.cs`, `SpaceColonyPlanner/README.md` (mention rename)

**Interfaces:**
- Produces: `GET /colony/plan/{id}` → uniform shape with **`result`** (was `plan`); `progress` = `{ stage: "AnalyzingPlanet"|"DeterminingStructures"|"PlanningStructures"|"Synthesizing"|"Optimizing", structures: [{ type: string, priority: string, quantity: number, done: boolean }] }` (record `ColonyProgress`).

- [ ] **Step 1: Status endpoint:** `Exists`, completed-only output, rename `plan` → `result`, add `progress`.
- [ ] **Step 2: Progress.** Publish `AnalyzingPlanet` first, `DeterminingStructures` after the planet analysis, then after `DetermineStructuresActivity` publish `PlanningStructures` with one `structures` entry per request (`done: false`).
- [ ] **Step 3: Worker completion.** Replace `Task.WhenAll(workerTasks)` by a `WhenAny` loop over `(index, task)` pairs; when a task finishes flip `done` for its index and `SetCustomStatus`; keep `structurePlans` in request order for `SynthesizePlanActivity`. Publish `Synthesizing` and `Optimizing` before the last two activities.
- [ ] **Step 4: Build and verify live.** `dotnet build SpaceColonyPlanner/SpaceColonyPlanner.sln` → `Build succeeded.`; with Procedure A start the Arrakis payload from the `.http` file and poll.
  Expected: `progress.structures` appears with the planet-specific list and `done` flags flip to `true` in any order, then `Synthesizing`, `Optimizing`, `Completed`; `result.structures` order equals the request order of `structures`. Unknown id → 404; terminated run → 200, `result: null`.

---

### Task 6: SpaceColonyPlanner — Web project, AppHost, README

**Files:**
- Create: `SpaceColonyPlanner/SpaceColonyPlanner.Web/**` (copy as Task 2 Step 1)
- Modify: AppHost `.cs`/`.csproj`, `.sln`, `SpaceColonyPlanner/README.md`

**Interfaces:**
- Consumes: Task 0 contract; Task 5 `ColonyProgress`; payload `ColonyRequest` (see spec Demo 3 for the nested shape and enum values); result `ColonyMasterPlan` (`structures[]`, `totalConstructionDays`, `materialsRequired`, `timeline[]` of `{ phaseNumber, name, structures, durationDays }`, `successFactors`, `riskAssessment`).

- [ ] **Step 1: Copy and rename.** **Step 2: `index.html`:** title "Colony Planning"; grouped fieldsets PLANET (`#planetId`, `#planetName`), CONDITIONS (`#gravity`, `#atmosphereType` select None|Toxic|Breathable|Thin, `#temperature`, `#radiationLevel`, `#hasWater` checkbox, `#dayLength`), RESOURCES (checkboxes `#metals`, `#rareEarths`, `#resWater`, `#organics`, `#uranium`; `#soilQuality` select None|Poor|Fair|Good), REQUIREMENTS (`#initialPopulation`, `#targetPopulation`, `#purpose` select Mining|Research|Agriculture|Military|Mixed, `#yearsToComplete`). Presets `arrakis`, `glacius`, `agri-world` (the third is new: a breathable, good-soil, agriculture/mixed planet).
- [ ] **Step 3: `demo.js`.** `start.path` `/api/colony/plan`, `statusPath` `/api/colony/plan/<id>`; `build()` assembles the nested `ColonyRequest` (numbers parsed, enums as strings; validate target ≥ initial population). `renderProgress`: two pre-step bars, a worker lane list built from `progress.structures` (`PLANNING`→`COMPLETE`), two post-step bars from `stage`. `renderResult`: headline numbers, timeline bars scaled to the longest phase (`width = durationDays / max * 100%`), structure list (expandable spec), materials table, `successFactors` and `riskAssessment` text; `alert` false always.
- [ ] **Step 4: `demo.css`, Step 5: AppHost/sln, Step 6: README** (document the `plan` → `result` rename) as in Task 2.
- [ ] **Step 7: Verify.** Build → `Build succeeded.`; Procedure A + `common.mjs` + demo checks: the `arrakis` and `glacius` presets produce different numbers of worker lanes; lanes complete in any order; the timeline has one bar per phase; `target < initial` population blocks the start with a form error.

---

### Task 7: AlienTranslator — ApiService

**Files:**
- Modify: `AlienTranslator/AlienTranslator.ApiService/Program.cs`, `Workflows/AlienTranslationWorkflow.cs`

**Interfaces:**
- Produces: `GET /translate/{id}` → uniform shape (`completedAt` is removed; the shape is exactly `{ instanceId, status, result, progress, createdAt, lastUpdatedAt }`); `progress` = `{ iterations: [{ iteration: number, translatedText: string, overallQuality: number, meetsStandards: boolean }], threshold: 8.0, maxIterations: 5 }` (record `TranslationProgress`).

- [ ] **Step 1: Status endpoint:** `Exists`, completed-only output, `progress`.
- [ ] **Step 2: Progress.** At the start of `RunAsync` publish the iterations derived from `input.Translations`/`input.Evaluations` (so a `ContinueAsNew` restart does not blank the status); after each evaluation republish with the new iteration appended; `threshold` and `maxIterations` come from `QUALITY_THRESHOLD` and `MAX_ITERATIONS`. Remove the old string status.
- [ ] **Step 3: Build and verify live.** `dotnet build AlienTranslator/AlienTranslator.sln` → `Build succeeded.`; with Procedure A start the Klingon payload (`context`: `"Literary"` string) and poll every second.
  Expected: `progress.iterations` grows by one per loop, never shrinks or empties across iterations, ends `Completed`; `result.totalIterations` consistent with the iteration count. Unknown id → 404; terminated → 200, `result: null`.

---

### Task 8: AlienTranslator — Web project, AppHost, README

**Files:**
- Create: `AlienTranslator/AlienTranslator.Web/**` (copy as Task 2 Step 1)
- Modify: AppHost `.cs`/`.csproj`, `.sln`, `AlienTranslator/README.md`

**Interfaces:**
- Consumes: Task 0 contract; Task 7 `TranslationProgress`; payload `AlienText` (`textId`, `alienSpecies`, `originalText`, `context` Diplomatic|Scientific|Literary|Casual, `knownVocabulary: { phrase: meaning }`, `culturalNotes`); result `AlienTranslationWorkflowOutput`.

- [ ] **Step 1: Copy and rename.** **Step 2: `index.html`:** title "Universal Translator"; `#alienSpecies` (select Vulcan|Klingon|Romulan|Other with a text input shown for Other), `#context`, `#originalText`, vocabulary rows (`#vocabulary`, `#add-vocab`; empty rows ignored, a half-filled row is a form error), `#culturalNotes`. Presets `vulcan`, `klingon`, `romulan` (from the `.http` file).
- [ ] **Step 3: `demo.js`.** `start.path` `/api/translate`, `statusPath` `/api/translate/<id>`; `build()` uses `textId: <SPECIES>-<uuid>`. `renderProgress`: one row per iteration (expandable translation text, quality bar 0-10 with a threshold marker from `progress.threshold`, verdict `MEETS STANDARD`/`BELOW STANDARD`), a trailing `PROCESSING ITERATION n` row while running, `ITERATION n / maxIterations` footer; keep last non-null progress. `renderResult`: original text beside final translation, four score bars (`accuracyScore`, `culturalNuanceScore`, `idiomaticScore`, `overallQuality`), strengths/weaknesses lists, `detailedFeedback`, `improvementSummary`, an SVG sparkline of `overallQuality` per iteration (plain `<polyline>`), `note` `MANUAL REVIEW RECOMMENDED` when the summary contains "Max iterations reached"; `alert` false.
- [ ] **Step 4: `demo.css`, Step 5: AppHost/sln, Step 6: README.**
- [ ] **Step 7: Verify.** Build → `Build succeeded.`; Procedure A + `common.mjs` + demo checks: a preset run shows at least one iteration row appearing while running and the final result; (if the model passes on the first try, run the Romulan preset or lower the displayed threshold check by using `window.renderResult` on a fabricated multi-iteration result to exercise the multi-row view); a vocabulary row with only a phrase blocks the start.

---

### Task 9: SpaceDebrisAgent — ApiService

**Files:**
- Modify: `SpaceDebrisAgent/SpaceDebrisAgent.ApiService/Program.cs`, `Workflows/SpaceDebrisCleanupWorkflow.cs`

**Interfaces:**
- Produces: `GET /mission/status/{id}` → uniform shape (add `lastUpdatedAt`); `progress` = `{ step: number, maxSteps: 15, thinking: boolean, decisions: [{ step: number, action: string, reasoning: string }], awaitingApproval: { since: string (ISO UTC), request: object } | null }` (record `MissionProgress`). `POST /mission/{id}/approval` unchanged.

- [ ] **Step 1: Status endpoint:** `Exists`, completed-only output (`MissionResult`), `progress`, `lastUpdatedAt`. Leave `GET /mission/{id}/decisions` as is except for the `Exists` check and a null-result guard returning an empty list.
- [ ] **Step 2: Progress.** Publish at the start of each iteration (`thinking: true`, decisions mapped from `input.Decisions`), after the decision (`thinking: false`, newest included), immediately before `WaitForExternalEventAsync` with `awaitingApproval: { since: context.CurrentUtcDateTime, request: decision.ActionParameters }`, and again with `awaitingApproval: null` after the event or timeout. Remove the old string status.
- [ ] **Step 3: Build and verify live.** `dotnet build SpaceDebrisAgent/SpaceDebrisAgent.sln` → `Build succeeded.`; with Procedure A start the LEO payload (`requireHumanApproval: true`) and poll once per second.
  Expected: `progress.step` increments, `decisions` accumulates across steps and never resets after `ContinueAsNew`; if the agent requests approval, `awaitingApproval` is non-null until you POST `{ "approved": true, "reason": "ok" }` to `/mission/<id>/approval`, then it becomes null; the run ends `Completed`. Unknown id → 404; terminated → 200, `result: null`.

---

### Task 10: SpaceDebrisAgent — Web project, AppHost, README

**Files:**
- Create: `SpaceDebrisAgent/SpaceDebrisAgent.Web/**` (copy as Task 2 Step 1)
- Modify: AppHost `.cs`/`.csproj`, `.sln`, `SpaceDebrisAgent/README.md`

**Interfaces:**
- Consumes: Task 0 contract (including `onPolled`); Task 9 `MissionProgress`; payload `MissionParameters` (`missionId`, `orbitalZone` LEO|MEO|GEO, `maxDebrisPieces`, `fuelBudget`, `maxMissionHours`, `requireHumanApproval`); approval endpoint `POST /api/mission/{id}/approval` body `{ approved: boolean, reason: string }`; result `MissionResult`.

- [ ] **Step 1: Copy and rename.** **Step 2: `index.html`:** title "Orbital Salvage Operations"; `#missionId` (generated, editable), `#orbitalZone`, `#maxDebrisPieces`, `#fuelBudget`, `#maxMissionHours`, `#requireHumanApproval` checkbox. Presets `leo`, `meo`, `geo`. Progress panel: `#step-meter`, `#decision-log`, `#approval-panel` (hidden by default) with `#approval-request`, `#approval-countdown`, `#approval-reason`, `#approve`, `#deny`.
- [ ] **Step 3: `demo.js`.** `start.path` `/api/mission/start`, `statusPath` `/api/mission/status/<id>`. `renderProgress`: step meter `STEP n / maxSteps`, decision log entries (action pill, expandable `reasoning`, `THINKING…` on the active step), approval panel shown iff `awaitingApproval` non-null with the request as key/values and a countdown from `since + 5 min`; the panel and its handlers are reset when `awaitingApproval` is null or the run is reset (`resetUi`). `#approve`/`#deny` POST once (buttons disabled while sending; on failure show the error and re-enable). `renderResult`: success/aborted banner, counters (`debrisCaptured`, `fuelUsed`, `totalSteps`), `summary`, `lessonsLearned` list, decision list and tool calls (`toolName`, parameters, `success`, `errorMessage`) in expandable rows; `alert: !result.success`.
- [ ] **Step 4: `demo.css`, Step 5: AppHost/sln, Step 6: README.**
- [ ] **Step 7: Verify.** Build → `Build succeeded.`; Procedure A + `common.mjs` + demo checks: the decision log grows while running and entries persist after a loop restart; to exercise the approval panel deterministically, call the demo's `renderProgress("Running", { ..., awaitingApproval: {...} })` through the console and check countdown, one POST per click, error re-enable (intercept the POST with a 500), and closing when `awaitingApproval` becomes null; with a real run (`requireHumanApproval: true`) approve one run and deny another if the agent asks (record if the model never asks); a denied run ends `MISSION ABORTED` with Red Alert.

---

### Task 11: Cross-project documentation

**Files:**
- Modify: `AGENTS.md`, root `README.md`

- [ ] **Step 1: `AGENTS.md`.** Change the optional `<Name>.Web` rule to apply to all six demos and state that each demo's front-end is self-contained: duplication across the Aspire projects is intended, nothing is shared.
- [ ] **Step 2: Root `README.md`.** Add one sentence that every demo ships a TNG-styled web UI at `http://localhost:5501` when started with `aspire run`.
- [ ] **Step 3: Final check.** Run: `for p in AlienTranslator AnomalyAnalysis GalacticAnomalyClassifier SpaceColonyPlanner SpaceDebrisAgent StarshipDiagnostics; do dotnet build $p/$p.sln; done` → `Build succeeded.` six times; `git status` shows no leftover `conversation.yaml` and every `Resources/` folder contains only `conversation-openai.yaml`, `env-secret.yaml`, `statestore.yaml`.

---

## Self-Review Notes

- **Spec coverage:** shared architecture, copy-not-share, engine/demo contract and Step 0 (Task 0); uniform status contract, lessons carried over, per-demo ApiService changes and progress shapes (Tasks 1, 3, 5, 7, 9); per-demo UI, presets and result views (Tasks 2, 4, 6, 8, 10); AppHost wiring, READMEs (each Web task); `AGENTS.md`/root README (Task 11); verification (Procedures A/B and each task's Verify step); rollout order matches the spec (Galactic, Starship, Colony, Alien, Debris).
- **Ruling recorded here:** the spec lists `engine.js` + `demo.js`; this plan adds `util.js` (`$`, `clean`, `levelFrom`) only to avoid a circular import between `engine.js` and `demo.js`.
- **Type consistency:** the config contract and DOM ids are defined once in Task 0 and referenced by Tasks 2, 4, 6, 8 and 10; progress record names and fields are defined in the ApiService task of each demo and consumed unchanged by its Web task.
- **Tests:** the repo has no test projects, so each task ends in a build and a live scripted check with stated expected results, not automated tests.
