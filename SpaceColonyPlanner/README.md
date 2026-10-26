# Space Colony Planner

This demo implements an **orchestrator-worker workflow** using Dapr Workflow for dynamic colony construction planning. The orchestrator analyzes a planet's unique conditions and dynamically determines which specialist workers are needed to create a comprehensive colony construction plan. Different planets require different structures, making this an ideal use case for the orchestrator-worker pattern.

## Pattern Overview

The **orchestrator-worker pattern** dynamically decomposes complex tasks into specialized subtasks executed by parallel workers. This demo implements a dynamic colony planning workflow:

1. **Orchestrator** analyzes planet conditions and determines required structures
2. **Dynamic Task Decomposition** creates the appropriate set of worker tasks
3. **Parallel Workers** execute specialized planning for each structure type
4. **Synthesis** combines worker outputs into a coherent master plan

### Key Features

- **Dynamic task decomposition** - The orchestrator determines which structures are needed based on planet conditions
- **Specialist workers** - Each structure type has a dedicated expert planner
- **Parallel execution** - Worker tasks run concurrently using Task.WhenAll
- **Intelligent synthesis** - Results are combined into a coherent construction timeline
- **Scalable complexity** - Automatically adjusts to simple or complex colony requirements

### Benefits

- ✅ Parallel execution reduces overall latency
- ✅ Specialized workers optimized for specific domains
- ✅ Dynamic task decomposition adapts to varying requirements
- ✅ Easy to add new worker types without changing orchestrator logic
- ✅ Clear separation between coordination and execution

### Drawbacks

- ❌ More complex orchestration logic
- ❌ Overhead from coordination and synthesis steps
- ❌ Requires careful state management across parallel workers
- ❌ Potential resource contention with many parallel workers

### When to Use

Use this pattern when tasks can be decomposed into independent subtasks, when different subtasks require specialized expertise, or when parallelization provides significant performance benefits. Ideal for complex planning, distributed analysis, and modular system design.

## Architecture

```mermaid
graph TD
    Start([Workflow Start: ColonyRequest]) --> A1[AnalyzePlanetActivity]
    A1 --> A2[DetermineStructuresActivity]
    A2 --> Router{Dynamic Structure Routing}
    Router -.->|HabitatDome| W1[PlanHabitatDomeActivity]
    Router -.->|PowerPlant| W2[PlanPowerPlantActivity]
    Router -.->|Agriculture| W3[PlanAgricultureActivity]
    Router -.->|MiningFacility| W4[PlanMiningFacilityActivity]
    Router -.->|ResearchLab| W5[PlanResearchLabActivity]
    Router -.->|DefenseSystem| W6[PlanDefenseSystemActivity]
    W1 --> Gather[Task.WhenAll - Gather Worker Results]
    W2 --> Gather
    W3 --> Gather
    W4 --> Gather
    W5 --> Gather
    W6 --> Gather
    Gather --> A3[SynthesizePlanActivity]
    A3 --> A4[OptimizeTimelineActivity]
    A4 --> End([Return ColonyMasterPlan])
    
    style Start fill:#e1f5ff
    style End fill:#d4edda
    style Router fill:#fff3cd
    style Gather fill:#fff3cd
    style W1 fill:#cfe2ff
    style W2 fill:#cfe2ff
    style W3 fill:#cfe2ff
    style W4 fill:#cfe2ff
    style W5 fill:#cfe2ff
    style W6 fill:#cfe2ff
```

- **ColonyOrchestratorWorkflow** - Main orchestrator that coordinates the planning process
- **Analysis Activities** - Analyze planet, determine structures, and synthesize the master plan
- **Worker Activities** - Specialized planners for different structure types (habitat domes, power plants, agriculture, etc.)

## Running the Demo

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download)
- [Aspire CLI](https://aspire.dev/get-started/install-cli/) — install with `dotnet tool install -g Aspire.Cli`
- [Docker](https://www.docker.com/products/docker-desktop/) or [Podman](https://podman.io/docs/installation)
- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/) (version 1.19+)
- An [OpenAI API key](https://platform.openai.com/api-keys)

### Configure OpenAI

The Dapr Conversation component in the `Resources` folder of the AppHost (`conversation-openai.yaml`) uses OpenAI with model `gpt-4.1-mini`. It reads the API key from the `OPENAI_API_KEY` environment variable through the `env-secret.yaml` secret store, so set it before starting the app:

```bash
export OPENAI_API_KEY=<your-key>
```

### Run the Application

From the `SpaceColonyPlanner/` folder:

```bash
aspire run
```

This launches the Aspire AppHost, which orchestrates:
- A Valkey container for workflow state persistence (port 16379, password-protected)
- The ApiService with a Dapr sidecar (app ID `space-colony-planner-app`)
- The `web` resource (LCARS web UI) on http://localhost:5504
- The Diagrid Dapr Dev Dashboard (`dapr-dev-dashboard` resource) on http://localhost:9090/workflows

The Aspire dashboard opens automatically in the browser at https://localhost:15000 (the same port for every demo), showing all resources and their status.

### Use the web UI

Open http://localhost:5504, pick a preset (Arrakis Minor, Glacius Prime or Eden Prime) or edit the survey, and choose **Plan colony**. The form is grouped as planet, conditions, resources (checkboxes plus the soil quality) and requirements. The page polls the plan endpoint every 2 seconds. The running view shows the planet analysis and structure selection as bars, then one lane per structure the orchestrator selected (type, priority and quantity), so the number of lanes differs per planet. Each lane sweeps as `PLANNING` until its specialist worker finishes and then shows `COMPLETE`, in whatever order the workers finish. Synthesis and timeline optimisation follow as two more bars. The result view shows the total construction days and the number of structures, a bar per construction phase scaled to the longest phase, expandable structure rows (materials, days, worker hours, prerequisites and specification), the materials table, and the success factors and risk assessment text.

**Durable timeline.** A strip of beads under the progress heading shows the saved steps of the run: one bead per saved step (planet analysis, structure selection, one per structure planned, synthesis, timeline optimisation); the ghost beads for the expected total appear once the number of structures is known. A bead flashes `saved` when it appears while you watch. The strip is driven by the status the workflow reports, not by a timer.

**Orbiting workers and building blocks.** A small planet is circled by one orbit ring per structure (at most 8 rings are drawn), and each ring turns while its structure is being planned and stops, with a solid dot, when its worker is done. Every lane also has a row of six building blocks that shimmer while the worker is planning and fill in when it finishes.

**Show the crash.** Start a run, then stop only the ApiService resource `wf-app` while the run is going: use the Stop action for `wf-app` in the Aspire dashboard at https://localhost:15000, or run `aspire resource wf-app stop`. Within a few seconds (typically 5 to 7) the page shows `Subspace link lost: state is safe`, the current bead turns red, the beads freeze and a counter shows how long the link has been down. Start `wf-app` again (the dashboard, or `aspire resource wf-app start`). The page reports that the link is restored and how many checkpoints were saved, and the run continues with the saved steps kept. The step that was running at the moment of the crash is executed again. The animations respect the browser's reduced-motion setting and show the final state at once.

**Token usage.** Each activity that calls the LLM returns its result wrapped in `Metered<T>` together with the prompt and completion tokens the provider reported. The workflow adds these up and publishes the running total as `usage` (`promptTokens`, `completionTokens`, `totalTokens`) in its custom status, so the status endpoint returns it as `progress.usage`. The progress panel shows a running **Tokens** readout that counts up as polls arrive, and the findings panel shows the final total with the prompt/completion split. If the provider reports no usage (null or zero), the page shows `--`. Tokens of failed or retried attempts are not counted. Only the activities that call the LLM report tokens: 3 of the 6 worker activities, plus the analysis, structure and synthesis steps that use the LLM. The other 3 workers (Mining, Defense, Research) and the Unknown fallback return `Metered<T>` with zero usage so every worker returns the same type. Runs that were in flight before upgrading to this version fail on replay (the activity outputs changed to `Metered<T>`), so let them finish or reset the state store (stop the app and clear Valkey) first.

### Test with REST Client

Open `SpaceColonyPlanner.ApiService/SpaceColonyPlanner.ApiService.http` in VS Code and execute the requests to plan different colonies. The ApiService HTTP port is shown in the Aspire dashboard.

The status endpoint `GET /colony/plan/{instanceId}` returns `result` (the master plan, only when the workflow is `Completed`; this field was called `plan` before) and `progress` (the current stage and the list of structures with a `done` flag). An unknown instance ID returns 404.

### Inspect the Workflow runs

Open the Diagrid Dapr Dev Dashboard at `http://localhost:9090/workflows` and inspect the workflow executions.

## Inspect runs with the Diagrid Dapr Dev Dashboard

The [Diagrid Dapr Dev Dashboard](https://github.com/diagridio/dev-dashboard) is an optional local tool that finds the apps you start with `dapr run` or Aspire and shows their workflow instances (status and execution history), sidecars, components, subscriptions and logs.

Install it once:

- macOS / Linux:

  ```shell
  curl -sSL https://raw.githubusercontent.com/diagridio/dev-dashboard/main/scripts/install.sh | sh
  ```

- Windows (PowerShell):

  ```powershell
  iwr -useb https://raw.githubusercontent.com/diagridio/dev-dashboard/main/scripts/install.ps1 | iex
  ```

`aspire run` starts it as the `dapr-dev-dashboard` resource; open it from the Aspire dashboard or at http://localhost:9090/workflows. If it is not installed, only that resource fails to start; the app keeps running.
