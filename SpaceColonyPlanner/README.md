# Space Colony Planner

This demo implements an **orchestrator-worker workflow** using Dapr Workflow for dynamic colony construction planning.

## Overview

The orchestrator analyzes a planet's unique conditions and dynamically determines which specialist workers are needed to create a comprehensive colony construction plan. Different planets require different structures, making this an ideal use case for the orchestrator-worker pattern.

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

1. Dapr CLI installed and initialized
2. Ollama running locally with llama3.2:latest model
3. .NET 9 SDK

### Start Ollama

```bash
ollama serve
```

### Run the Application

```bash
dapr run -f .
```

### Test the Workflow

Use the VSCode REST Client with `local.http` to:
1. POST a colony planning request with planet data
2. GET the status and results of your colony plan

## Key Features

- **Dynamic task decomposition** - The orchestrator determines which structures are needed based on planet conditions
- **Specialist workers** - Each structure type has a dedicated expert planner
- **Intelligent synthesis** - Results are combined into a coherent construction timeline
- **Scalable complexity** - Automatically adjusts to simple or complex colony requirements
