# AnomalyAnalysis front-end — design spec

**Date:** 2026-10-18
**Scope:** Add a Star Trek TNG (LCARS) themed web front-end to the `AnomalyAnalysis` Aspire solution. It starts the workflow, shows its progress and shows the result.

## Goal

A browser UI, started by `aspire run` together with the rest of the solution, that lets a demo audience:

1. Enter (or pick a preset for) a spatial anomaly and **start** the `AnomalyAnalysisWorkflow`.
2. Watch the workflow **progress** stage by stage while the LLM-powered activities run.
3. Read the **result**: anomaly type, scientific analysis, risk level and tactical recommendation, with a Red Alert treatment when the risk is critical.

## Non-goals

- Changing workflow logic, activities or models.
- Authentication, persistence, history of past runs, or multi-user concerns.
- Replacing the Diagrid Dapr Dev Dashboard. It stays the tool for deep inspection (full history, sidecar, logs).
- Using Star Trek logos, images or audio from the shows. The look is recreated with CSS only.

## Existing back-end surface

From `AnomalyAnalysis.ApiService.http` and `Program.cs` (ApiService listens on `http://localhost:5500`):

| Method | Path | Request | Response |
|--------|------|---------|----------|
| POST | `/anomaly/analyze` | `SpatialAnomaly` JSON | `202 Accepted`, `{ "instanceId": "ANOM-<anomalyId>" }` |
| GET | `/anomaly/status/{instanceId}` | - | `200 { instanceId, status, result, createdAt, lastUpdatedAt }` or `404` |

`SpatialAnomaly`:

```json
{
  "anomalyId": "<guid>",
  "rawSensorData": "SUBSPACE_SIGNATURE: 47.3 THz | GRAVIMETRIC_DISTORTION: 8.2 tesla | ...",
  "spatialCoordinates": "Sector 001, Alpha Quadrant",
  "detectedAt": "2370-06-15T14:32:00Z",
  "sensorReadings": {
    "subspaceFrequency": 47.3,
    "gravimetricDistortion": 8.2,
    "temporalVariance": 0.003,
    "radiationLevel": 2.1
  }
}
```

`result` is an `AnalysisResult` (`anomalyId`, `stages[]`, `anomalyType`, `scientificAnalysis`, `riskLevel`, `tacticalRecommendation`). `status` is the Dapr `WorkflowRuntimeStatus` string (`Running`, `Completed`, `Failed`, `Terminated`, `Pending`, `Suspended`).

### Gap: progress is not exposed

The workflow calls `context.SetCustomStatus(stages)` after every stage, but the status endpoint only returns the final output. While the workflow is `Running`, `result` is empty, so the UI cannot show progress. This spec adds the custom status to the status response (see "ApiService change").

The workflow has five recorded stages, in order:

1. `ProcessSensorDataActivity`
2. `ClassifyAnomalyActivity`
3. `ScientificAnalysisActivity`
4. `RiskAssessmentActivity`
5. `GenerateRecommendationActivity`

`AlertBridgeActivity` runs conditionally (risk contains "CRITICAL") between stage 4 and 5 and is not added to the stages list. The UI infers the Red Alert from the final `riskLevel`, not from a stage.

## Front-end technique decision

**Chosen: a new `AnomalyAnalysis.Web` ASP.NET Core project that serves a vanilla HTML/CSS/JS single page from `wwwroot`, and forwards `/api/*` to the ApiService with YARP.**

Why this fits the repository:

- `AGENTS.md` requires vanilla HTML/CSS/JS, no front-end frameworks, small and lightweight code. No Node toolchain, bundler or `npm install` is needed, so `aspire run` still only needs the .NET SDK, Docker and the Dapr CLI.
- A separate project follows the Aspire pattern the solution already uses (`AddProject` per service) and appears as its own resource with a clickable URL in the Aspire dashboard. It keeps the ApiService free of UI files.
- The proxy removes CORS configuration and the hard-coded `localhost:5500`: the browser only talks to the Web project, which reaches the ApiService through Aspire service discovery (`WithReference`).
- Same SDK, target (net10.0) and `AddServiceDefaults()` conventions as the other projects.

Alternatives considered:

| Option | Why not |
|--------|---------|
| Static files in `ApiService/wwwroot` | Smallest change, but mixes UI into the workflow service and does not show up as a separate Aspire resource. |
| Vite/Node app via `Aspire.Hosting.JavaScript` | Violates the no-framework/lightweight rule and adds a Node prerequisite. |
| Blazor | A framework, against `AGENTS.md`; the LCARS look is easier to get with plain CSS. |
| Two hand-written forwarding endpoints instead of YARP | No extra package, but more C# than YARP's configuration. Acceptable fallback if the YARP package is a problem. |

## Architecture

```
Browser ──▶ AnomalyAnalysis.Web (static files + YARP /api/*)
                    │  service discovery (WithReference)
                    ▼
            AnomalyAnalysis.ApiService (wf-app + Dapr sidecar) ──▶ Valkey
```

### New project: `AnomalyAnalysis.Web`

```
AnomalyAnalysis.Web/
├── AnomalyAnalysis.Web.csproj        Microsoft.NET.Sdk.Web, net10.0, refs ServiceDefaults + Yarp.ReverseProxy + Microsoft.Extensions.ServiceDiscovery.Yarp
├── Program.cs
├── appsettings.json                  YARP route/cluster config
├── Properties/launchSettings.json
└── wwwroot/
    ├── index.html
    ├── css/lcars.css
    ├── js/app.js
    └── fonts/Antonio-*.woff2         self-hosted so the demo works offline
```

`Program.cs` (sketch):

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver();

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapReverseProxy();
app.MapDefaultEndpoints();
app.Run();
```

`appsettings.json`: route `/api/{**catch-all}` with a path-remove-prefix transform of `/api` to cluster destination `http://wf-app`. So `POST /api/anomaly/analyze` reaches `POST /anomaly/analyze`.

### AppHost change

```csharp
builder.AddProject<Projects.AnomalyAnalysis_Web>("web")
    .WithReference(workflowApp)
    .WaitFor(workflowApp)
    .WithExternalHttpEndpoints();
```

Add the project to `AnomalyAnalysis.sln` and as a project reference in the AppHost `.csproj`. Pick a fixed HTTP port (suggest `5501`) in `launchSettings.json`, next to the ApiService's `5500`, so the URL in the README is stable.

### ApiService change

Extend the status response in `Program.cs` with the stages recorded through `SetCustomStatus`:

```csharp
progress = state.ReadCustomStatusAs<List<AnalysisStage>>()
```

Everything else is unchanged. The `.http` file needs no new request (the status call already exists); update the README to mention the extra `progress` field. `AnalysisStage` stays in `Models/`.

### Front-end behaviour (`app.js`)

State machine: `IDLE → SUBMITTING → RUNNING → COMPLETED | FAILED`.

1. **Start:** read the form, build `SpatialAnomaly` (new `crypto.randomUUID()` per run so instance IDs never collide), `POST /api/anomaly/analyze`, keep `instanceId`.
2. **Poll:** `GET /api/anomaly/status/{instanceId}` every 2 s (`setTimeout` chain, not `setInterval`, so slow LLM calls don't overlap). Tolerate one or two `404`s right after scheduling.
3. **Progress:** the UI has a fixed list of the five stages with display names. A stage is *done* if it appears in `progress`, *active* if it is the first not done while `status` is `Running`, otherwise *pending*.
4. **Stop** polling on `Completed`, `Failed` or `Terminated`; on network errors show an LCARS-style "SUBSPACE LINK LOST" banner and keep retrying with a slower interval.
5. **Result:** render `anomalyType`, `scientificAnalysis`, `riskLevel` and `tacticalRecommendation`. If `riskLevel` contains `CRITICAL` (case-insensitive) switch the page to Red Alert styling. If `anomalyType` is `UNKNOWN` (the workflow's gate-check failure path, which still reports `Completed`) show the `scientificAnalysis` text as the failure reason.
6. **Presets:** a few buttons fill the form (the wormhole sample from the `.http` file, a temporal rift with high `temporalVariance`, a high-radiation quantum fluctuation) so a live demo needs no typing, and one of them reliably triggers a critical risk.
7. Render all server text with `textContent`, never `innerHTML` (LLM output is untrusted).

## UI design (LCARS, TNG era)

### Visual language

- Black background; panels as solid colour blocks with large rounded corners and "elbow" shapes joining the top bar and the sidebar.
- TNG palette: orange `#FF9900`, peach `#FFCC99`, lilac `#CC99CC`, blue-violet `#9999CC`, sky `#99CCFF`, salmon `#CC6666`, gold `#FFCC66`; text on blocks in black, data text in orange or peach.
- Font: Antonio (an open Swiss 911 lookalike), all caps, wide letter-spacing, right-aligned labels inside the pill buttons.
- Decorative numbers inside the side blocks (e.g. `03-1701`, `47-9120`), a stardate in the header computed from the current date, and a faint pulsing element on the active stage.
- Optional, off by default: short WebAudio beeps on submit and completion, behind a `SOUND` toggle. No audio files are shipped.
- Layout uses CSS grid/flex with variables for the palette, responsive down to a tablet width; elbows collapse into a plain bar on narrow screens.

### Suggested layout

**Idle: enter sensor data**

```
┌──────────────┐ ANOMALY ANALYSIS ───────────────────────── STARDATE 47634.2 ─┐
│  01-4712     ╲_______________________________________________________________│
│              │                                                                │
│  SENSOR DATA │  SENSOR INPUT                                                  │
├──────────────┤  ┌────────────────────────────────────────────────────────┐   │
│  ANALYSIS    │  │ COORDINATES  [ Sector 001, Alpha Quadrant            ] │   │
├──────────────┤  │ DETECTED AT  [ 2370-06-15T14:32:00Z                  ] │   │
│  FINDINGS    │  │ SUBSPACE (THz) [47.3]  GRAVIMETRIC (T) [8.2]           │   │
├──────────────┤  │ TEMPORAL (%)  [0.003]  RADIATION       [2.1]           │   │
│  SOUND  ○    │  │ RAW SENSOR LOG [ SUBSPACE_SIGNATURE: 47.3 THz | ...    ]│   │
│              │  └────────────────────────────────────────────────────────┘   │
│  PRESETS     │  (WORMHOLE) (TEMPORAL RIFT) (QUANTUM FLUX)    ( BEGIN ANALYSIS )│
│  47-9120     │                                                                │
└──────────────┴────────────────────────────────────────────────────────────────┘
```

**Running: stage-by-stage progress**

```
│  ANALYSIS IN PROGRESS            INSTANCE ANOM-3f9c…        STATUS: RUNNING   │
│                                                                                │
│  01 SENSOR DATA PROCESSING   ████████████████████████████████  COMPLETE       │
│  02 ANOMALY CLASSIFICATION   ████████████████████████████████  COMPLETE       │
│  03 SCIENTIFIC ANALYSIS      ██████████████▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒  PROCESSING…     │
│  04 RISK ASSESSMENT          ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░  STANDBY         │
│  05 TACTICAL RECOMMENDATION  ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░  STANDBY         │
│                                                                                │
│  ELAPSED 00:42        Each completed stage expands to show its output ▾        │
```

Completed stages are expandable pills showing the stage output (from `progress`), so the audience can follow the chain of LLM calls. The active stage's bar animates segment by segment.

**Completed: findings**

```
│  FINDINGS                                         RISK LEVEL  ┌──────────────┐ │
│                                                               │  MODERATE    │ │
│  CLASSIFICATION   TRAVERSABLE WORMHOLE (STABLE)               └──────────────┘ │
│  ───────────────────────────────────────────────────────────────────────────── │
│  SCIENTIFIC ANALYSIS                                                            │
│  Subspace signature of 47.3 THz and a gravimetric distortion of 8.2 tesla …    │
│  ───────────────────────────────────────────────────────────────────────────── │
│  TACTICAL RECOMMENDATION                                                        │
│  Maintain yellow alert; dispatch a science team and a probe before transit …   │
│                                          ( NEW ANALYSIS )  ( VIEW STAGES )     │
```

**Critical: Red Alert**

The whole palette switches to salmon/red, the header reads `RED ALERT`, the risk block pulses slowly, and a banner reads `BRIDGE HAS BEEN NOTIFIED`, mirroring what `AlertBridgeActivity` does on the server. Respect `prefers-reduced-motion` by disabling the pulse.

### Other suggestions (optional)

- A thin "SUBSPACE LINK" indicator in the footer bar, green while polling succeeds, amber on retries.
- A "VIEW IN DAPR DASHBOARD" pill linking to `http://localhost:9090/workflows`, to connect the UI to the Dev Dashboard in demos.
- A tiny `LCARS` easter egg: clicking the header elbow cycles between the TNG palette and a "nemesis" (blue/violet) palette.

## Documentation updates

- `AnomalyAnalysis/README.md`: describe the Web project, add its URL (http://localhost:5501) to the list of resources started by `aspire run`, note the `progress` field.
- `AGENTS.md`: the "three csprojs" rule becomes "three csprojs, plus an optional `<Name>.Web` front-end project (AnomalyAnalysis only)".
- Root `README.md`: no change needed.

## Verification

1. `dotnet build AnomalyAnalysis.sln` succeeds.
2. `aspire run` shows the `web` resource healthy and the URL opens the LCARS page.
3. Manual run: starting the wormhole preset shows five stages filling in order, then the findings; a critical preset shows Red Alert; reloading mid-run is out of scope (the instance ID is not persisted).
4. Compare with the Diagrid Dapr Dev Dashboard (http://localhost:9090/workflows): the same instance ID with matching stage outputs.
5. Check the browser console for errors and that no server text is injected as HTML.

The repository has no automated test projects, so no unit tests are added.

## Prerequisites and risks

- The LLM call must work for a meaningful demo. `AnomalyAnalysis/AnomalyAnalysis.AppHost/Resources` contains both `conversation.yaml` (Ollama) and `conversation-openai.yaml`, which share the component name `conversation`. Keep only one before running.
- Polling every 2 s is deliberately simple. If a Dapr workflow is slow to schedule, the first status call can return `404`; the UI must retry instead of failing.
- LLM output varies, so the `CRITICAL` match is string-based and may not trigger on every run. The critical preset should use sensor values that push the model that way, but this cannot be guaranteed.
- Adding YARP service discovery adds two NuGet packages (`Yarp.ReverseProxy`, `Microsoft.Extensions.ServiceDiscovery.Yarp`); versions are chosen at implementation time to match the other Aspire packages.
