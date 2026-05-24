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
- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/) (version 1.17+)
- [Ollama](https://ollama.com/) with the `llama3.2:3b` model pulled

### Start Ollama

```bash
ollama serve
ollama pull llama3.2:3b
```

### Run the Application

From the `SpaceColonyPlanner/` folder:

```bash
aspire run
```

This launches the Aspire AppHost, which orchestrates:
- A Valkey container for workflow state persistence (port 16379, password-protected)
- The ApiService with a Dapr sidecar (app ID `space-colony-planner-app`)
- The Diagrid Dev Dashboard container on http://localhost:18080

The Aspire dashboard opens automatically in the browser, showing all resources and their status.

### Test with REST Client

Open `SpaceColonyPlanner.ApiService/SpaceColonyPlanner.ApiService.http` in VS Code and execute the requests to plan different colonies. The ApiService HTTP port is shown in the Aspire dashboard.

### Inspect the Workflow runs

Open the Diagrid Dev Dashboard at `http://localhost:18080` and inspect the workflow executions.
