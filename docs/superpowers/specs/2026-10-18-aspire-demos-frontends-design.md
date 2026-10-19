# LCARS front-ends for the remaining Aspire demos — design spec

**Date:** 2026-10-18
**Scope:** Add a Star Trek TNG (LCARS) web UI project to each of the five remaining Aspire demos, reusing the implementation built for `AnomalyAnalysis`. Each UI starts the workflow, shows its progress while it runs and shows the output.

**Reference implementation:** `AnomalyAnalysis/AnomalyAnalysis.Web` and the spec `docs/superpowers/specs/2026-10-18-anomaly-analysis-frontend-design.md`.

## Goal

For each of these demos a `<Name>.Web` project, started by `aspire run` next to the existing resources, that fits the demo's use case:

| Demo | Workflow pattern shown | UI concept |
|------|------------------------|------------|
| `GalacticAnomalyClassifier` | Routing (classify, then one specialist) | Stellar cartography console with a routing board |
| `StarshipDiagnostics` | Parallel scans + voting | Engineering console with subsystem tiles and a vote panel |
| `SpaceColonyPlanner` | Orchestrator-workers (dynamic fan-out) | Colonization office with a worker board and a build timeline |
| `AlienTranslator` | Evaluator-optimizer loop (`ContinueAsNew`) | Universal translator with an iteration/quality board |
| `SpaceDebrisAgent` | Autonomous agent loop + human approval | Operations console with a decision log and an approval panel |

`AnomalyAnalysis` stays as is, except for the engine/demo split described in "Step 0".

## Non-goals

- Changing workflow business logic (activities, prompts, models) beyond publishing progress through `SetCustomStatus`.
- Authentication, persistence, run history, multi-user concerns.
- Replacing the Diagrid Dapr Dev Dashboard, which remains the tool for deep inspection.
- Star Trek logos, images or audio from the shows; the look is CSS only.
- Automated tests: the repo has no test projects. Verification is a build plus a scripted headless-Chrome run per demo (see "Verification").

## Lessons carried over from AnomalyAnalysis

These were found while building and reviewing the reference UI and are requirements here:

1. `DaprWorkflowClient.GetWorkflowStateAsync` never returns `null`. Every status endpoint must use `if (!state.Exists) return Results.NotFound();`. All five demos currently have the dead `state == null` check, so an unknown instance returns `200 { status: "Unknown" }`.
2. Read the workflow output only when `state.RuntimeStatus == WorkflowRuntimeStatus.Completed`. A terminated run can carry a plain string output, and deserializing it makes the endpoint return 500.
3. LLM text is markdown-flavoured and long. Render it with `textContent` after stripping `**` and leading `#` markers; never `innerHTML`. Do not assume a field the model fills is a short label.
4. The status endpoint must publish progress, because `result` is empty while the workflow runs. Progress travels as the workflow custom status (`SetCustomStatus`) and the status endpoint returns it as `progress`.
5. The UI must survive: a `404` right after scheduling (retry, give up after 30 s), network errors and bad bodies (link-lost banner, slower retry, recovery), a double click on start, a new run while an old poll is in flight, `progress: null`, and `Failed`/`Terminated` runs (failed view with a way out).
6. Move keyboard focus to the active panel heading on every view change; use live regions for status text; honour `prefers-reduced-motion`.
7. Stop and restart `aspire` with about 10 seconds between them, or the Valkey container name clashes.

## Architecture (same for every demo)

```
Browser ──▶ <Name>.Web (static files + YARP /api/*) ──service discovery──▶ <Name>.ApiService (wf-app + Dapr sidecar) ──▶ Valkey
```

Identical in all six solutions (verified): the ApiService resource is `wf-app`, listens on `http://localhost:5500`, and `Resources/` holds only `conversation-openai.yaml`, `env-secret.yaml` and `statestore.yaml`.

### Reuse strategy: copy, don't share

The repo is a teaching repo where each demo is a fully independent solution and clarity beats DRY across projects. So there is **no shared project, linked folder or shared package**, and no rule that the copies must stay in sync. Each `<Name>.Web` starts as a copy of the reference and may diverge freely afterwards (a demo can change its engine or CSS without touching any other demo). Inside each project the front-end is still split into a generic engine and a small per-demo file, which keeps the demo-specific code short and readable:

```
<Name>.Web/
├── <Name>.Web.csproj              copied from the reference, names changed
├── Program.cs                     copied (static files + YARP)
├── appsettings.json               copied (route /api/{**catch-all} -> http://wf-app)
├── Properties/launchSettings.json copied, port 5501
└── wwwroot/
    ├── index.html                 per demo: form fields, panels, labels
    ├── css/lcars.css              copied (frame, palette, pills, banners, stage bars)
    ├── css/demo.css               per demo: only what the demo's panels need
    ├── js/engine.js               copied (start, poll, link state, views, focus, helpers)
    ├── js/demo.js                 per demo: config object + render functions
    └── fonts/Antonio.woff2        copied
```

Nothing is shared at build time or at run time: every solution builds, runs and is understood on its own.

### Engine contract (`engine.js` imports `demo.js`)

`engine.js` owns everything that does not depend on the use case. `demo.js` exports one default `config` object:

```js
export default {
  start:   { path: "/api/<start>", build(): object },        // build() reads the form, throws Error(message) when invalid
  status:  (instanceId) => `/api/<status>/${instanceId}`,
  instanceIdFrom: (response) => response.instanceId,          // default: response.instanceId
  presets: { <name>: { label, apply(form) } },                // preset buttons fill the form
  renderProgress(status, progress),                           // draws the running state; progress is demo-defined or null
  renderResult(result),                                       // draws the output; returns { alert: boolean, note?: string }
  alertLabel: "Red alert — ...",                              // banner text when renderResult reports alert
  idle: { focus: "form-panel" }                               // optional extras
};
```

The engine provides the state machine (`idle → running → completed | failed`), the 2 s `setTimeout` poll chain (5 s after errors, 30 s of consecutive 404s to give up), the link indicator, the elapsed clock, view switching with focus handling, `clean(text)`, `tokenFor(run)` cancellation, and the New analysis / restart buttons. Optional hooks: `onRunning(statusState)` (used by SpaceDebrisAgent to enable the approval panel) and `actions` (extra buttons wired by `demo.js`).

### Uniform status contract

Every ApiService status endpoint returns the same shape, so every copy of the engine can stay generic:

```json
{ "instanceId": "...", "status": "Running|Completed|Failed|Terminated|...",
  "result": <final output or null>, "progress": <custom status or null>,
  "createdAt": "...", "lastUpdatedAt": "..." }
```

Today's differences to normalize:

| Demo | Status route | Current shape | Change |
|------|--------------|---------------|--------|
| GalacticAnomalyClassifier | `GET /anomaly/status/{id}` | `result`, no progress | add `progress`, `Exists`, completed-only output |
| StarshipDiagnostics | `GET /ship/report/{id}` | `result`, no progress | same |
| SpaceColonyPlanner | `GET /colony/plan/{id}` | **`plan`**, no progress | rename `plan` to `result` (API change, update README), add `progress` |
| AlienTranslator | `GET /translate/{id}` | `result`, `completedAt`, custom status is a string | `progress` becomes structured (below); `completedAt` is removed (shape: `{ instanceId, status, result, progress, createdAt, lastUpdatedAt }`) |
| SpaceDebrisAgent | `GET /mission/status/{id}` | `result`, no `lastUpdatedAt` | add `progress`, `lastUpdatedAt` |

Update each `<Name>.ApiService.http` only if a route or payload changes (only the Colony `result` rename affects documentation, not requests). Also add `JsonStringEnumConverter` to `GalacticAnomalyClassifier` (`ConfigureHttpJsonOptions`, as the Alien, Colony and Debris projects already do) so `result.type` serializes as `TemporalRift` and not as a number.

### AppHost wiring (each demo)

```csharp
builder
    .AddProject<Projects.<Name>_Web>("web")
    .WithReference(workflowApp)
    .WaitFor(workflowApp)
    .WithExternalHttpEndpoints();
```

Add the project to the `.sln` and as a `ProjectReference` in the AppHost csproj. Port `5501` in every demo (only one solution runs at a time, like the existing 5500/16379/9090).

### Common look and feel

Same LCARS language as the reference (palette variables, Antonio, elbows, pills, stage bars, pulsing alert, reduced-motion). Per demo only these change: the page title and header text, the three sidebar labels and decorative numbers, which alert state exists (Red Alert is used where the demo has a danger outcome), and the panels in the main area. Every demo has the same footer (link indicator, "View in Dapr dashboard" to `http://localhost:9090/workflows`), the same view model (`idle` form, `running`, `completed`, `failed`) and the same restart button.

## Step 0 — put AnomalyAnalysis on the engine/demo split

Move the generic parts of `AnomalyAnalysis.Web/wwwroot/js/app.js` into `engine.js` and the anomaly-specific parts (presets, `buildAnomaly`, stage list, `riskLabel`, `renderResult`) into `demo.js`, with `index.html` loading `js/engine.js`. Behaviour must not change. This makes `AnomalyAnalysis` the starting point the other five copy; afterwards each project owns its copy. Verify with the existing scripted flows (full run, link lost, terminate, 404, XSS, restart, focus) before any other demo is touched. If any check regresses, stop and fix the split before continuing.

## Demo 1 — GalacticAnomalyClassifier (routing)

**Endpoints:** `POST /anomaly/analyze` (`SpaceAnomaly`: `anomalyId`, `sensorData`, `coordinates`, `detectedAt`, `measurements: { name: number }`), `GET /anomaly/status/{id}`. The instance id equals `anomalyId`. The UI generates `CLASS-<uuid>`. `/anomalies/{id}` and `/anomalies/stats` are not used (stats are hard-coded).

**Form:** sensor data (textarea), coordinates, detected at, and a dynamic list of measurement rows (name + number, add/remove) because the keys differ per anomaly type. **Presets** (the five in the `.http` file): Temporal rift, Dark matter cluster, Alien artifact, Stellar phenomenon, Dimensional tear.

**Workflow change for progress:** `AnomalyRoutingWorkflow` has no custom status. Set it at start `{ "stage": "Classifying" }` and after classification `{ "stage": "Analyzing", "type": "<AnomalyType>", "confidence": 0.0 }`.

**Running view:** a routing board. A `CLASSIFIER` node on the left feeds five lanes (Temporal rift, Dark matter, Alien artifact, Stellar phenomenon, Dimensional tear). While `Classifying` the classifier pulses and all lanes are dim. When `type` arrives the matching lane lights up and shows `ANALYZING`, the other lanes grey out with `NOT ROUTED`; an `Unknown` type shows no lane lit and a note.

```
 CLASSIFIER ████████ COMPLETE  confidence 0.92
   ├── TEMPORAL RIFT        ▓▓▒▒▒▒  ANALYZING
   ├── DARK MATTER CLUSTER  ░░░░░░  NOT ROUTED
   ├── ALIEN ARTIFACT       ░░░░░░  NOT ROUTED
   ├── STELLAR PHENOMENON   ░░░░░░  NOT ROUTED
   └── DIMENSIONAL TEAR     ░░░░░░  NOT ROUTED
```

**Result view (`AnalysisResult`):** type pill, threat level pill, the classification text, specialized analysis, a key/value table for `metrics` (`Dictionary<string, object>`; render values with `textContent`, round numbers), and `recommendations` as a list. **Alert:** the Red Alert theme when `threatLevel` contains `CRITICAL` or `SEVERE` (same label-parsing rule as the reference: parse a known level from the text, do not match the whole text). `Unknown` type shows the "manual review" note.

## Demo 2 — StarshipDiagnostics (parallel scans + voting)

**Endpoints:** `POST /ship/diagnose` (`Starship`: `shipId`, `name`, `class`, `yearsInService`, `lastMaintenance`, `telemetry` with `hullStressLevel`, `reactorTemperature`, `shieldIntegrity`, `oxygenLevel`, `navigationAccuracy`, `weaponsPower`), `GET /ship/report/{id}`. `GET /ship/{shipId}/history` is not used. Instance id is `DIAG-<shipId>-<ticks>`, so repeated runs for one ship are fine.

**Form:** ship id, name, class, years in service, last maintenance, six telemetry fields (range sliders with numeric readout; reactor temperature as a number). **Presets:** Enterprise-D, Enterprise (original, aging), Enterprise-E (well maintained), matching the `.http` file.

**Workflow change for progress:** the five scans and the votes run under `Task.WhenAll`, so nothing is observable until all finish. Replace each `WhenAll` by a `WhenAny` loop (replay-safe for durable tasks) that records each completed task and calls `SetCustomStatus`:
`{ "stage": "Scanning|Voting|Aggregating", "scans": [{ "activity": "HullIntegrityScanActivity", "subsystem": "...", "status": "...", "healthPercentage": 0 }], "votes": [{ "category": "...", "consensus": "...", "agreementLevel": 0 }] }`. The aggregation logic and results stay identical; only the way results are collected changes.

**Running view:** five subsystem tiles (Hull integrity, Reactor core, Navigation, Life support, Weapons). A tile is `SCANNING` (animated) until its `activity` appears in `scans`, then shows health % and a status colour (nominal, warning, critical). When critical findings exist a vote panel appears: one row per finding with the three voters (Safety, Severity, Recommendation) and, once done, the consensus and agreement %.

```
 ┌ HULL INTEGRITY ┐ ┌ REACTOR CORE ┐ ┌ NAVIGATION ┐ ┌ LIFE SUPPORT ┐ ┌ WEAPONS ┐
 │ 67%  WARNING   │ │ SCANNING…    │ │ 95%  OK    │ │ 98%  OK      │ │ 88%  OK │
 └────────────────┘ └──────────────┘ └────────────┘ └──────────────┘ └─────────┘
 VOTING — REACTOR CORE   SAFETY ✔  SEVERITY ✔  RECOMMENDATION …   CONSENSUS —
```

**Result view (`DiagnosticReport`):** a large departure indicator (`CLEAR FOR DEPARTURE` or `DEPARTURE HELD`) from `clearForDeparture`, overall status pill, per-subsystem health bars with issues and recommendations (expandable, `detailedAnalysis` as plain text), vote results with agreement, required repairs and estimated repair hours. **Alert:** Red Alert when `clearForDeparture` is false or `overallStatus` parses to critical.

## Demo 3 — SpaceColonyPlanner (orchestrator-workers)

**Endpoints:** `POST /colony/plan` (`ColonyRequest`: `planet` with `planetId`, `name`, `conditions` {`gravity`, `atmosphereType` (None|Toxic|Breathable|Thin), `temperature`, `radiationLevel`, `hasWater`, `dayLength`}, `resources` {`metals`, `rareEarths`, `water`, `organics`, `uranium`, `soilQuality` (None|Poor|Fair|Good)}; `requirements` {`initialPopulation`, `targetPopulation`, `purpose` (Mining|Research|Agriculture|Military|Mixed), `yearsToComplete`}), `GET /colony/plan/{id}` (response key `plan` renamed to `result`, see the contract table). Enums are sent as strings (`JsonStringEnumConverter` is already registered).

**Form:** grouped as PLANET, CONDITIONS, RESOURCES, REQUIREMENTS with selects for the enums and checkboxes for the booleans. **Presets:** Arrakis Minor (desert, mining) and Glacius Prime (ice world, research) from the `.http` file, plus one agriculture/mixed preset added for variety (three buttons).

**Workflow change for progress:** set the custom status at each step `{ "stage": "AnalyzingPlanet|DeterminingStructures|PlanningStructures|Synthesizing|Optimizing", "structures": [{ "type": "HabitatDome", "priority": "Critical", "quantity": 2, "done": false }] }`. `structures` appears after `DetermineStructuresActivity` (this is the dynamic part: the number of workers differs per planet). Replace the `Task.WhenAll(workerTasks)` by a `WhenAny` loop that flips `done` as each worker finishes and republishes the status; the plans are still collected in the original order.

**Running view:** a worker board built dynamically from `structures`: one lane per requested structure (type, priority, quantity), each `PLANNING` then `COMPLETE`. Above it the two pre-steps and below it Synthesize and Optimize as stage bars.

```
 01 PLANET ANALYSIS      ██████████ COMPLETE
 02 STRUCTURE SELECTION  ██████████ COMPLETE  → 5 workers
 03 SPECIALIST PLANNING  HABITAT DOME ✔  POWER PLANT ✔  MINING FACILITY …  DEFENSE …  UNKNOWN …
 04 SYNTHESIS            ░░░░░░░░░░ STANDBY
 05 TIMELINE OPTIMIZATION░░░░░░░░░░ STANDBY
```

**Result view (`ColonyMasterPlan`):** headline numbers (total construction days, structures), a construction timeline drawn as horizontal LCARS bars per `ConstructionPhase` (name, structures, duration, scaled to the longest), a structure list with materials, days, worker hours, prerequisites and the specification (expandable), a materials table (`materialsRequired`), and the success factors and risk assessment text. No Red Alert; a risk text that parses as high is shown with the salmon accent only.

## Demo 4 — AlienTranslator (evaluator-optimizer loop)

**Endpoints:** `POST /translate` (`AlienText`: `textId`, `alienSpecies`, `originalText`, `context` (Diplomatic|Scientific|Literary|Casual), `knownVocabulary: { phrase: meaning }`, `culturalNotes`), `GET /translate/{id}`. The instance id is `TRANS-<textId>`; the UI generates a unique `textId` (`<SPECIES>-<uuid>`) per run. `GET /translate/{id}/iteration/{n}` and `/translations/...` in the `.http` file are not implemented in the ApiService and are not used.

**Form:** species (select Vulcan, Klingon, Romulan, or free text), context select, original text, a dynamic known-vocabulary list (phrase + meaning rows), cultural notes. **Presets:** Vulcan diplomatic, Klingon literary, Romulan casual (from the `.http` file).

**Workflow change for progress:** the workflow loops with `ContinueAsNew` (max 5 iterations, quality threshold 8.0). Replace the string custom status with `{ "iterations": [{ "iteration": 1, "translatedText": "...", "overallQuality": 6.5, "meetsStandards": false }], "threshold": 8.0, "maxIterations": 5 }`, published after each evaluation **and at the start of each `RunAsync`** from `input.Translations`/`input.Evaluations`, so the status does not go empty right after a `ContinueAsNew`. The engine additionally keeps the last non-null `progress` it has seen.

**Running view:** an iteration board. Each iteration is a row: its translation text (expandable), a quality bar (0 to 10) with a marker at the threshold, and a verdict (`BELOW STANDARD` / `MEETS STANDARD`). While `Running` a final row shows `PROCESSING ITERATION n` (Translate or Refine, then Evaluate). A footer shows `ITERATION n / 5`.

```
 ITERATION 1  ██████▒▒▒▒ 6.5 │ 8.0   BELOW STANDARD    ▸ translation
 ITERATION 2  █████████▒ 8.4 │ 8.0   MEETS STANDARD    ▸ translation
 ITERATION 3  PROCESSING…
```

**Result view (`AlienTranslationWorkflowOutput`):** the original text next to the final translation, the four scores of the final evaluation (accuracy, cultural nuance, idiomatic, overall) as bars, strengths and weaknesses lists, detailed feedback, the improvement summary and a small quality-per-iteration sparkline drawn with CSS/SVG (no chart library). If the summary says max iterations were reached, show the `MANUAL REVIEW RECOMMENDED` note.

## Demo 5 — SpaceDebrisAgent (agent loop with human approval)

**Endpoints:** `POST /mission/start` (`MissionParameters`: `missionId`, `orbitalZone` (LEO|MEO|GEO), `maxDebrisPieces`, `fuelBudget` (kg), `maxMissionHours`, `requireHumanApproval`), `GET /mission/status/{id}`, `POST /mission/{id}/approval` (`{ approved, reason }`). Instance id is `CLEAN-<missionId>`; the UI generates `<ZONE>-<uuid>`. `GET /mission/{id}/decisions` throws while the mission runs (output is null), so the UI does not use it; decisions come from `progress` and, at the end, from `result.decisions`.

**Form:** mission id (generated, editable), orbital zone select, max debris pieces, fuel budget, max mission hours, a "require human approval" switch. **Presets:** LEO cleanup (approval on), MEO aggressive (approval off), GEO conservative.

**Workflow change for progress:** the current custom status is the string `Step: n/15`. Replace it with `{ "step": 3, "maxSteps": 15, "thinking": true, "decisions": [{ "step": 1, "action": "SCAN_DEBRIS_FIELD", "reasoning": "..." }], "awaitingApproval": null }`. Publish it at the start of each iteration (`thinking: true`, decisions carried from `input.Decisions`), after the decision (`thinking: false`, newest decision included), immediately before `WaitForExternalEventAsync` with `awaitingApproval: { "since": "<context.CurrentUtcDateTime>", "request": <ActionParameters> }`, and again with `awaitingApproval: null` once the event or timeout resolved.

**Running view:** a decision log that grows as the agent works: one entry per step with the chosen action as a pill (`SCAN_DEBRIS_FIELD`, `ANALYZE_DEBRIS`, `MOVE_TO_LOCATION`, `CHECK_FUEL`, `CAPTURE_DEBRIS`, `REQUEST_HUMAN_APPROVAL`, `COMPLETE_MISSION`) and the agent's reasoning (expandable), a step meter `STEP n / 15`, and a `THINKING…` indicator on the active step. When `awaitingApproval` is set an **approval panel** takes the foreground: the request details (`request` as key/values), a countdown to the 5-minute timeout computed from `since`, a reason field and `APPROVE` / `DENY` buttons that `POST /api/mission/{id}/approval`. Both buttons are disabled after one click; a failed POST shows the error and re-enables them. When the status drops `awaitingApproval` (decision made or timed out) the panel closes. Because the `requireHumanApproval` flag only makes the agent *able* to ask, the UI does not assume an approval will happen.

```
 STEP 4 / 15      THINKING…
 01 SCAN_DEBRIS_FIELD   ▸ reasoning
 02 ANALYZE_DEBRIS      ▸ reasoning
 03 MOVE_TO_LOCATION    ▸ reasoning
 ┌─ HUMAN APPROVAL REQUIRED ─────────── 04:12 remaining ┐
 │ maneuver: close approach 120 m     risk: HIGH          │
 │ reason [..........................]  (APPROVE) (DENY) │
 └───────────────────────────────────────────────────────┘
```

**Result view (`MissionResult`):** success/aborted banner (`success`), counters (debris captured, fuel used, total steps), the summary text, lessons learned as a list, and the full decision list with the tool calls (`toolName`, parameters, `success`, error) in expandable rows. A denied approval or timeout shows as `MISSION ABORTED` with the reason; Red Alert styling is used for an unsuccessful mission.

## Documentation changes

- Each demo's README gets the same additions as the AnomalyAnalysis README: the `web` resource (http://localhost:5501) in the `aspire run` list, a "Use the web UI" section tailored to the demo, and the status `progress` field. The SpaceColonyPlanner README documents the `plan` to `result` rename.
- `AGENTS.md`: the optional `<Name>.Web` rule now applies to all six demos, and says each demo's front-end is self-contained: duplication across the Aspire projects is intended, nothing is shared.
- Root `README.md`: one sentence that every demo ships a TNG-styled web UI.

## Rollout order and phases

1. **Step 0:** engine/demo split of AnomalyAnalysis, verified (gate for everything else).
2. **GalacticAnomalyClassifier** (smallest change: one custom status, enum converter).
3. **StarshipDiagnostics** (introduces the `WhenAny` progress pattern).
4. **SpaceColonyPlanner** (reuses the `WhenAny` pattern, adds the `plan` rename).
5. **AlienTranslator** (structured custom status across `ContinueAsNew`).
6. **SpaceDebrisAgent** (human-approval interaction; most complex).
7. Documentation.

Each demo is its own independently shippable unit: ApiService fixes, progress publishing, Web project, AppHost wiring, README, verification.

## Verification

Per demo, reusing the scripted headless-Chrome approach that verified AnomalyAnalysis (`puppeteer-core` against the installed Google Chrome, driven from the scratchpad, never committed):

1. `dotnet build <Name>.sln` succeeds; `aspire run` shows `web` healthy on 5501.
2. Full run from the first preset: progress renders as designed and the result view matches the demo's output model. Screenshots of idle, running and completed are inspected.
3. The shared edge cases: link lost and recovery, double click starts one instance, 404 retry and give-up, bad JSON body, terminate (including `dapr workflow terminate --output '"..."'`), HTML in LLM text stays text, restart works from running and failed, focus lands on the new panel heading.
4. Demo-specific: Galactic routes to the matching lane and handles an `Unknown` type; Starship shows tiles completing out of order and a vote panel for a critical ship (original Enterprise preset); Colony builds a variable number of worker lanes for the two presets; Alien shows more than one iteration and survives a `ContinueAsNew` with no flicker to empty; Debris shows an approval request, approves one run and denies another, and exercises the timeout path by lowering the timeout in a test run only.
5. The ApiService changes are checked live: unknown instance returns 404, a terminated run returns `result: null` with 200, and the status shape matches the contract table.

**LLM for verification:** the demos need a working conversation component. For local verification without an OpenAI key, temporarily swap `conversation-openai.yaml` for an Ollama component (model `glm-5.3-flash:cloud` works; `qwen3.5:cloud` was retired) and restore the OpenAI file afterwards; do not leave the swap in the repo. Real runs use `OPENAI_API_KEY`.

## Risks and open points

- **Workflow edits change replay behaviour.** Replacing `WhenAll` by a `WhenAny` loop and adding `SetCustomStatus` calls is replay-safe, but running instances from before the change may replay differently. Demos are short-lived; clear the state volume or use new instance ids when testing after the change.
- **Custom status size.** SpaceDebrisAgent publishes the decision list each step (at most 15 entries with reasoning); AlienTranslator publishes up to five translations. Both are small, but the status is stored with the workflow state, so keep entries to what the UI shows.
- **LLM output variability.** Labels and levels (threat level, overall status, risk) are free text; the UI parses a known level and falls back to showing the text. A demo may therefore not reach its alert state on every run.
- **Colony `plan` rename** is a small API break for anyone calling the endpoint directly; the `.http` file does not read the field, so nothing in the repo breaks.
- **Approval flow in SpaceDebrisAgent** needs no SSE: the approval itself is a plain `POST` to the existing approval endpoint, and the request reaches the UI through the same status polling (`awaitingApproval` in the custom status, visible within about 2 s of a 5-minute window). The open point is different: the LLM decides whether the agent asks for approval at all, so the panel cannot be demonstrated deterministically. A later improvement could add a deterministic "force an approval step" option, which is out of scope here.
- **Six copies.** Duplication is accepted by design. A fix found in one copy of `engine.js` or `lcars.css` is not propagated automatically; apply it to the others by hand when it matters.
