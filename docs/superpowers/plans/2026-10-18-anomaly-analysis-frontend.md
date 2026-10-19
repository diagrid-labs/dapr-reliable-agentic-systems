# AnomalyAnalysis LCARS Front-end Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a Star Trek TNG (LCARS) themed web UI to the `AnomalyAnalysis` Aspire solution that starts the workflow, shows stage-by-stage progress by polling the status endpoint, and shows the result.

**Architecture:** A new `AnomalyAnalysis.Web` ASP.NET Core project serves a vanilla HTML/CSS/JS page from `wwwroot` and forwards `/api/*` to the ApiService (`wf-app`) with YARP and Aspire service discovery. The ApiService status endpoint is extended to return the workflow custom status as `progress`. The browser polls the status endpoint every 2 s.

**Tech Stack:** .NET 10, .NET Aspire 13.6.1, YARP (`Yarp.ReverseProxy` 2.3.0, `Microsoft.Extensions.ServiceDiscovery.Yarp` 10.10.0), vanilla HTML/CSS/JavaScript, Antonio font (self-hosted).

**Spec:** `docs/superpowers/specs/2026-10-18-anomaly-analysis-frontend-design.md`

## Global Constraints

- Vanilla HTML/CSS/JS only: no front-end frameworks, no Node toolchain, no bundler (`AGENTS.md`).
- All new projects target `net10.0`; the Web project uses `Microsoft.NET.Sdk.Web` and calls `builder.AddServiceDefaults()` first and `app.MapDefaultEndpoints()` before `app.Run()`.
- Keep code small and modular; do not introduce unnecessary classes or files. Comment only where logic is complex.
- No Star Trek logos, images or audio files; the look is CSS only. Antonio is self-hosted in `wwwroot/fonts` so the demo works offline.
- Polling, not SSE: `setTimeout` chain every 2 s, never `setInterval`.
- Server text (LLM output) is rendered with `textContent`, never `innerHTML`.
- Instance IDs: a new `crypto.randomUUID()` anomaly ID per run (server builds `ANOM-<anomalyId>`).
- Web project fixed HTTP port `5501`; ApiService stays on `5500`.
- Do not run `git add`, `git commit` or `git rm`; the user stages and commits (saved preference).
- The line-ending rules in `.gitattributes` (`eol=lf`) apply to all new files.

## Review Focus

- Status `404` right after scheduling: the UI retries instead of showing a failure.
- ApiService unreachable or network error mid-run: "SUBSPACE LINK LOST" banner, polling continues at a slower interval, recovers when the link returns.
- Workflow gate-check failure (`anomalyType` is `UNKNOWN`, status still `Completed`): the UI shows the failure reason, not an empty result.
- Workflow `Failed` / `Terminated`: polling stops and the UI shows a terminal state, with the completed stages still visible.
- Double click on "Begin analysis" or a second run after one finished: only one workflow per click, polling from the previous run is cancelled.
- `riskLevel` with mixed case or surrounding text ("Critical", "RISK: CRITICAL - ..."): Red Alert still triggers.
- LLM text containing HTML (`<img onerror=...>`): rendered as text.
- `progress` is `null` while the workflow is `Pending`/just started: the UI treats it as zero completed stages.

---

## File Structure

| File | Responsibility |
|------|----------------|
| `AnomalyAnalysis/AnomalyAnalysis.ApiService/Program.cs` (modify) | Add `progress` to the status response |
| `AnomalyAnalysis/AnomalyAnalysis.Web/AnomalyAnalysis.Web.csproj` | Web project, packages, ServiceDefaults reference |
| `AnomalyAnalysis/AnomalyAnalysis.Web/Program.cs` | Static files, YARP proxy, default endpoints |
| `AnomalyAnalysis/AnomalyAnalysis.Web/appsettings.json` | YARP route `/api/{**catch-all}` to `http://wf-app` |
| `AnomalyAnalysis/AnomalyAnalysis.Web/Properties/launchSettings.json` | Port 5501 |
| `AnomalyAnalysis/AnomalyAnalysis.AppHost/AppHost.cs` + `.csproj` (modify) | Register the `web` resource and project reference |
| `AnomalyAnalysis/AnomalyAnalysis.sln` (modify) | Include the Web project |
| `AnomalyAnalysis/AnomalyAnalysis.Web/wwwroot/index.html` | Page structure: form, progress list, findings, banners |
| `AnomalyAnalysis/AnomalyAnalysis.Web/wwwroot/css/lcars.css` | LCARS look, palette variables, Red Alert theme |
| `AnomalyAnalysis/AnomalyAnalysis.Web/wwwroot/js/app.js` | State machine, presets, polling, rendering |
| `AnomalyAnalysis/AnomalyAnalysis.Web/wwwroot/fonts/` | Antonio woff2 files |
| `AnomalyAnalysis/README.md`, `AGENTS.md` (modify) | Documentation |

---

### Task 1: Expose workflow progress in the status endpoint

**Files:**
- Modify: `AnomalyAnalysis/AnomalyAnalysis.ApiService/Program.cs` (the `/anomaly/status/{instanceId}` handler)

**Interfaces:**
- Produces: `GET /anomaly/status/{instanceId}` returns `{ instanceId, status, result, progress, createdAt, lastUpdatedAt }` where `progress` is `AnalysisStage[] | null` (`{ stageName, input, output, success, errorMessage }`, camelCase JSON) and equals the stages recorded by `SetCustomStatus`.

- [ ] **Step 1: Add the `progress` property**

In the anonymous response object add `progress = state.ReadCustomStatusAs<List<AnalysisStage>>()` between `result` and `createdAt`. `AnalysisStage` is already in `AnomalyAnalysis.Models` (already imported).

- [ ] **Step 2: Build**

Run: `dotnet build AnomalyAnalysis/AnomalyAnalysis.sln`
Expected: `Build succeeded.`

- [ ] **Step 3: Verify against a running workflow**

Run `aspire run` from `AnomalyAnalysis/`, then POST the sample payload from `AnomalyAnalysis.ApiService.http` to `http://localhost:5500/anomaly/analyze` and poll `GET /anomaly/status/ANOM-<id>` a few times with `curl`.
Expected: `status` is `Running` while `progress` grows from `[]`/`null` to 1, 2, 3... stage entries, and on `Completed` `progress` has 5 entries and `result` is populated. Stop with `aspire stop`.

---

### Task 2: Web project with proxy, wired into the AppHost

**Files:**
- Create: `AnomalyAnalysis/AnomalyAnalysis.Web/AnomalyAnalysis.Web.csproj`, `Program.cs`, `appsettings.json`, `Properties/launchSettings.json`, `wwwroot/index.html` (placeholder text only)
- Modify: `AnomalyAnalysis/AnomalyAnalysis.AppHost/AnomalyAnalysis.AppHost.csproj`, `AppHost.cs`, `AnomalyAnalysis/AnomalyAnalysis.sln`

**Interfaces:**
- Consumes: ApiService resource `wf-app` (Task 1's status shape).
- Produces: `http://localhost:5501/` serves `wwwroot`; `/api/<path>` reaches ApiService `/<path>` (so `POST /api/anomaly/analyze`, `GET /api/anomaly/status/{id}`). Later tasks only edit `wwwroot` files.

- [ ] **Step 1: Create the project**

`dotnet new web -n AnomalyAnalysis.Web -o AnomalyAnalysis/AnomalyAnalysis.Web --no-restore`, then edit the `.csproj` to match the ApiService's property conventions (net10.0, `ImplicitUsings`, `Nullable`, `RootNamespace` `AnomalyAnalysis`), add a project reference to `..\AnomalyAnalysis.ServiceDefaults\AnomalyAnalysis.ServiceDefaults.csproj`, and add `Yarp.ReverseProxy` 2.3.0 and `Microsoft.Extensions.ServiceDiscovery.Yarp` 10.10.0 package references. Add it to the solution with `dotnet sln AnomalyAnalysis/AnomalyAnalysis.sln add AnomalyAnalysis/AnomalyAnalysis.Web/AnomalyAnalysis.Web.csproj`.

- [ ] **Step 2: Write `Program.cs`**

Order: `builder.AddServiceDefaults()`, `AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy")).AddServiceDiscoveryDestinationResolver()`, build, `UseDefaultFiles()`, `UseStaticFiles()`, `MapReverseProxy()`, `MapDefaultEndpoints()`, `Run()`.

- [ ] **Step 3: Write `appsettings.json` and `launchSettings.json`**

`ReverseProxy`: one route `api` matching `/api/{**catch-all}` with transform `PathRemovePrefix: "/api"` to cluster `workflow` with destination `http://wf-app`. `launchSettings.json`: profile `http`, `applicationUrl` `http://localhost:5501`, `launchBrowser` false, `ASPNETCORE_ENVIRONMENT` `Development`. Create `wwwroot/index.html` with a one-line placeholder.

- [ ] **Step 4: Wire the AppHost**

Add `<ProjectReference>` to `..\AnomalyAnalysis.Web\AnomalyAnalysis.Web.csproj` in the AppHost `.csproj`. In `AppHost.cs`, after `workflowApp.WaitFor(cache);`, add `builder.AddProject<Projects.AnomalyAnalysis_Web>("web").WithReference(workflowApp).WaitFor(workflowApp).WithExternalHttpEndpoints();`.

- [ ] **Step 5: Build and verify the proxy**

Run: `dotnet build AnomalyAnalysis/AnomalyAnalysis.sln` — Expected: `Build succeeded.`
Run `aspire run`; `aspire describe` shows `web` as `Running`/`Healthy` at `http://localhost:5501`. Then `curl -s -X POST http://localhost:5501/api/anomaly/analyze -H 'Content-Type: application/json' -d @<sample payload>` returns `202` with an `instanceId`, and `curl http://localhost:5501/api/anomaly/status/<instanceId>` returns the Task 1 shape. `curl http://localhost:5501/` returns the placeholder page. Stop with `aspire stop`.

---

### Task 3: LCARS page structure and styling

**Files:**
- Create: `wwwroot/css/lcars.css`, `wwwroot/fonts/Antonio-Regular.woff2`, `wwwroot/fonts/Antonio-Bold.woff2`
- Modify: `wwwroot/index.html`

**Interfaces:**
- Produces (DOM contract used by Task 4; ids are fixed):
  - Header: `#stardate`, `#alert-banner` (hidden by default)
  - Form `#anomaly-form` with inputs `#coordinates`, `#detectedAt`, `#subspaceFrequency`, `#gravimetricDistortion`, `#temporalVariance`, `#radiationLevel`, `#rawSensorData`, submit button `#begin`, preset buttons `button[data-preset="wormhole|temporal-rift|quantum-flux"]`
  - Progress: `#progress-panel` containing `<ol id="stage-list">` with five `<li data-stage="ProcessSensorDataActivity|ClassifyAnomalyActivity|ScientificAnalysisActivity|RiskAssessmentActivity|GenerateRecommendationActivity">`, each with `.stage-name`, `.stage-bar`, `.stage-state`, `.stage-output`; plus `#instance-id`, `#run-status`, `#elapsed`
  - Findings: `#findings-panel` with `#anomaly-type`, `#risk-level`, `#scientific-analysis`, `#tactical-recommendation`, `#new-analysis` button
  - Footer: `#link-indicator`, `#link-lost` banner, link to `http://localhost:9090/workflows` ("View in Dapr Dashboard")
  - Panel visibility via `data-view` on `<body>`: `idle | running | completed | failed`; Red Alert via `body.red-alert`.

- [ ] **Step 1: Write `index.html`** with the structure above: top bar with elbow, left sidebar with decorative number blocks and labels (SENSOR DATA, ANALYSIS, FINDINGS), main area with the three panels. Static labels in the five `<li>` use the display names (SENSOR DATA PROCESSING, ANOMALY CLASSIFICATION, SCIENTIFIC ANALYSIS, RISK ASSESSMENT, TACTICAL RECOMMENDATION).

- [ ] **Step 2: Write `lcars.css`** with palette variables from the spec (`--orange #FF9900`, `--peach #FFCC99`, `--lilac #CC99CC`, `--blue #9999CC`, `--sky #99CCFF`, `--salmon #CC6666`, `--gold #FFCC66`), `@font-face` for Antonio, rounded elbow/pill shapes, `body[data-view]` rules showing only the matching panel(s), `body.red-alert` palette swap with a slow pulse on `#risk-level`, an animated segmented fill for the active stage, and `@media (prefers-reduced-motion: reduce)` disabling the animations. Responsive: elbows collapse to a plain bar below tablet width.

- [ ] **Step 3: Verify visually**

Run `aspire run`, open `http://localhost:5501/`, and with the browser dev tools set `document.body.dataset.view` to each of `idle`, `running`, `completed`, `failed` and toggle `body.red-alert`.
Expected: each view shows the right panels, no horizontal scroll at 768 px width, fonts load from `/fonts/` (Network tab, no external requests).

---

### Task 4: Front-end behaviour (start, poll, render)

**Files:**
- Create: `wwwroot/js/app.js` (loaded with `<script type="module" src="js/app.js">`; add the tag in `index.html`)

**Interfaces:**
- Consumes: Task 3's DOM contract; `POST /api/anomaly/analyze` and `GET /api/anomaly/status/{id}` (Task 1/2 shapes).
- Produces (module-internal functions, in this order):
  - `buildAnomaly(): SpatialAnomaly` — reads the form, new `crypto.randomUUID()` as `anomalyId`.
  - `applyPreset(name: "wormhole" | "temporal-rift" | "quantum-flux"): void` — wormhole is the sample from `AnomalyAnalysis.ApiService.http`; temporal-rift uses a high `temporalVariance`; quantum-flux uses a high `radiationLevel` and is intended to trigger a critical risk.
  - `startAnalysis(): Promise<void>` — disables `#begin` while submitting, POSTs, stores `instanceId`, cancels any running poll, sets `data-view="running"`, starts polling.
  - `poll(instanceId: string): Promise<void>` — `setTimeout` chain, 2000 ms between calls, 5000 ms after a network error; retries on `404`; stops on `Completed`, `Failed`, `Terminated`.
  - `renderProgress(status: string, progress: AnalysisStage[] | null): void` — stage is done if its `stageName` is in `progress`, active if it is the first not done while `status` is `Running`, else pending; done stages show their `output` via `textContent` in `.stage-output`.
  - `renderResult(result: AnalysisResult): void` — fills the findings; if `riskLevel` matches `/critical/i` add `body.red-alert` and show `#alert-banner` ("BRIDGE HAS BEEN NOTIFIED"); if `anomalyType === "UNKNOWN"` show `scientificAnalysis` as the failure reason.
  - `updateElapsed()` and a stardate formatter feeding `#elapsed` and `#stardate`.

- [ ] **Step 1: Implement form, presets and `startAnalysis`** as specified; wire `#anomaly-form` submit (preventDefault), preset buttons and `#new-analysis` (resets to `idle`, clears `red-alert`, cancels polling).

- [ ] **Step 2: Implement `poll`, `renderProgress`, `renderResult`** as specified, including the `#link-indicator` states (ok / retrying) and the `#link-lost` banner after the first network error, hidden again on the next successful response.

- [ ] **Step 3: Verify the happy path**

Run `aspire run` with a working conversation component (keep only one of `conversation.yaml` / `conversation-openai.yaml` in `AnomalyAnalysis.AppHost/Resources`; for OpenAI export `OPENAI_API_KEY`). Open `http://localhost:5501/`, click the wormhole preset, then "Begin analysis".
Expected: the view switches to running, the five stages complete in order with expandable output, the view then switches to completed with all four findings filled; the same instance ID appears in the Dev Dashboard at `http://localhost:9090/workflows`.

- [ ] **Step 4: Verify the Review Focus cases**

In the browser:
- Throttle the network to offline in dev tools mid-run → `#link-lost` shows, polling continues, the banner clears when back online.
- Double-click "Begin analysis" → the Dev Dashboard shows exactly one new instance.
- Run the quantum-flux preset → if the risk contains "critical" the page shows Red Alert and the banner; if the model does not return it, temporarily set a breakpoint to call `renderResult({ ..., riskLevel: "Risk: Critical" })` from the console and confirm the Red Alert.
- Call `renderResult({ anomalyId: "x", stages: [], anomalyType: "UNKNOWN", scientificAnalysis: "<img src=x onerror=alert(1)>", riskLevel: "UNKNOWN", tacticalRecommendation: "-" })` from the console → the markup appears as text and no alert fires.
- Terminate a running instance with the Dev Dashboard or `DaprWorkflowClient` → polling stops and the failed view shows the stages completed so far.
- Refresh immediately after starting (before the first stage) → the page returns to idle without errors in the console.

---

### Task 5: Documentation

**Files:**
- Modify: `AnomalyAnalysis/README.md`, `AGENTS.md`

- [ ] **Step 1: Update `AnomalyAnalysis/README.md`**: add `AnomalyAnalysis.Web` to the architecture description, add "The LCARS web UI on http://localhost:5501" to the list of resources `aspire run` starts, add a short "Use the web UI" section (open the URL, pick a preset, begin the analysis), and mention the new `progress` field in the status endpoint description. Keep the existing Dev Dashboard section.

- [ ] **Step 2: Update `AGENTS.md`**: change the "composed of three csprojs" rule to say three csprojs, plus an optional `<Name>.Web` project serving a vanilla JS front-end (currently AnomalyAnalysis only).

- [ ] **Step 3: Final check**

Run: `dotnet build AnomalyAnalysis/AnomalyAnalysis.sln` — Expected: `Build succeeded.` and no references to `18080` in `AnomalyAnalysis/README.md`.

---

## Self-Review Notes

- **Spec coverage:** technique and architecture (Tasks 1-2), `progress` gap (Task 1), AppHost/sln/port (Task 2), UI design, presets, Red Alert, reduced motion, offline font (Task 3), behaviour and polling rules (Task 4), documentation updates (Task 5), verification list (Tasks 1, 2, 4, 5). The optional extras from the spec (sound toggle, palette easter egg) are intentionally left out (YAGNI); the Dev Dashboard link is included in Task 3.
- **Type consistency:** `progress`, `AnalysisStage` fields, DOM ids and the five stage names are defined once in Tasks 1 and 3 and reused unchanged in Task 4.
- **Tests:** the repository has no test projects (the spec says none are added), so each task ends in a build and/or a manual verification with exact expected outcomes instead of automated tests.
