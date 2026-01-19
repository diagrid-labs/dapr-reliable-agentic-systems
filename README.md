# Build reliable agentic systems with Dapr Workflow

This repository demonstrates how to build reliable agentic systems by combining Dapr Workflow orchestration with LLM-powered activities using the Dapr Conversation API.

## Overview

The demos showcase the patterns described in [Building Effective Agents](https://www.anthropic.com/engineering/building-effective-agents) by Anthropic, implemented using Dapr Workflow to orchestrate reliable multi-step processes and the Dapr Conversation API to interact with LLMs. Ollama is used as the local LLM provider for all demos.

## Benefits of Dapr Workflow and Conversation API

### Dapr Workflow

- **Deterministic control** over task execution with structured error handling and retries
- **Durable state management** that persists across failures and restarts
- **Built-in orchestration patterns** for complex multi-step processes
- **Reliable execution** with automatic recovery and compensation logic

### Dapr Conversation API

- **Flexibility and natural language understanding** through LLM integration
- **Provider-agnostic** interface supporting OpenAI, Azure OpenAI, Ollama, and more
- **Simplified LLM integration** with consistent API across different providers
- **Built-in caching** to optimize performance and reduce costs

### Combined Power

By combining deterministic workflows with the probabilistic nature of LLMs, you can build agentic systems that are both reliable and flexible, leveraging the best of both worlds.

## Agentic Patterns

| Pattern | Demo Project |
|---------|-------------|
| [Prompt Chaining](AnomalyAnalysis/README.md) | AnomalyAnalysis |
| [Routing](GalacticAnomalyClassifier/README.md) | GalacticAnomalyClassifier |
| [Parallelization](StarshipDiagnostics/README.md) | StarshipDiagnostics |
| [Orchestrator-Workers](SpaceColonyPlanner/README.md) | SpaceColonyPlanner |
| [Evaluator-Optimizer](AlienTranslator/README.md) | AlienTranslator |
| [Autonomous Agent](SpaceDebrisAgent/README.md) | SpaceDebrisAgent |

## Pattern Architectures

### Prompt Chaining - AnomalyAnalysis

```mermaid
graph TD
    Start([Workflow Start: SpatialAnomaly]) --> A1[ProcessSensorDataActivity]
    A1 --> Gate1{Data Valid?}
    Gate1 -->|No| Fail([Return Failed Result])
    Gate1 -->|Yes| A2[ClassifyAnomalyActivity]
    A2 --> A3[ScientificAnalysisActivity]
    A3 --> A4[RiskAssessmentActivity]
    A4 --> Gate2{Risk = CRITICAL?}
    Gate2 -->|Yes| Alert[AlertBridgeActivity]
    Gate2 -->|No| A5[GenerateRecommendationActivity]
    Alert --> A5
    A5 --> End([Return AnalysisResult])
    
    style Start fill:#e1f5ff
    style End fill:#d4edda
    style Fail fill:#f8d7da
    style Alert fill:#fff3cd
    style Gate1 fill:#fff3cd
    style Gate2 fill:#fff3cd
```

### Routing - GalacticAnomalyClassifier

```mermaid
graph TD
    Start([Workflow Start: SpaceAnomaly]) --> A1[ClassifyAnomalyActivity]
    A1 --> Router{Anomaly Type?}
    Router -->|Temporal Rift| A2[AnalyzeTemporalRiftActivity]
    Router -->|Dark Matter| A3[AnalyzeDarkMatterActivity]
    Router -->|Alien Artifact| A4[AnalyzeAlienArtifactActivity]
    Router -->|Stellar Phenomenon| A5[AnalyzeStellarPhenomenonActivity]
    Router -->|Dimensional Tear| A6[AnalyzeDimensionalTearActivity]
    A2 --> End([Return Analysis Result])
    A3 --> End
    A4 --> End
    A5 --> End
    A6 --> End
    
    style Start fill:#e1f5ff
    style End fill:#d4edda
    style Router fill:#fff3cd
    style A2 fill:#cfe2ff
    style A3 fill:#cfe2ff
    style A4 fill:#cfe2ff
    style A5 fill:#cfe2ff
    style A6 fill:#cfe2ff
```

### Parallelization - StarshipDiagnostics

```mermaid
graph TD
    Start([Workflow Start: Starship]) --> Parallel{Parallel Scans}
    Parallel -->|Parallel| S1[HullIntegrityScanActivity]
    Parallel -->|Parallel| S2[ReactorCoreScanActivity]
    Parallel -->|Parallel| S3[NavigationScanActivity]
    Parallel -->|Parallel| S4[LifeSupportScanActivity]
    Parallel -->|Parallel| S5[WeaponsScanActivity]
    S1 --> Gather[Gather Results]
    S2 --> Gather
    S3 --> Gather
    S4 --> Gather
    S5 --> Gather
    Gather --> Gate{Critical Findings?}
    Gate -->|Yes| Vote{Parallel Voting}
    Gate -->|No| Aggregate[AggregateResultsActivity]
    Vote -->|Parallel| V1[SafetyVoterActivity]
    Vote -->|Parallel| V2[SeverityVoterActivity]
    Vote -->|Parallel| V3[RecommendationVoterActivity]
    V1 --> GatherVotes[Gather Votes]
    V2 --> GatherVotes
    V3 --> GatherVotes
    GatherVotes --> Aggregate
    Aggregate --> End([Return DiagnosticReport])
    
    style Start fill:#e1f5ff
    style End fill:#d4edda
    style Parallel fill:#fff3cd
    style Gate fill:#fff3cd
    style Vote fill:#fff3cd
    style S1 fill:#cfe2ff
    style S2 fill:#cfe2ff
    style S3 fill:#cfe2ff
    style S4 fill:#cfe2ff
    style S5 fill:#cfe2ff
    style V1 fill:#ffd6cc
    style V2 fill:#ffd6cc
    style V3 fill:#ffd6cc
```

### Orchestrator-Workers - SpaceColonyPlanner

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

### Evaluator-Optimizer - AlienTranslator

```mermaid
graph TD
    Start([Workflow Start: AlienText]) --> Check1{First Iteration?}
    Check1 -->|Yes| A1[TranslateActivity]
    Check1 -->|No| A3[RefineTranslationActivity]
    A1 --> A2[EvaluateTranslationActivity]
    A3 --> A2
    A2 --> Gate1{Quality ≥ 8.0 AND<br/>Meets Standards?}
    Gate1 -->|Yes| Success([Return Success Result])
    Gate1 -->|No| Gate2{Max Iterations<br/>Reached?}
    Gate2 -->|Yes| MaxReached([Return Best Effort Result])
    Gate2 -->|No| Restart[ContinueAsNew<br/>Restart Workflow]
    Restart -.-> Check1
    
    style Start fill:#e1f5ff
    style Success fill:#d4edda
    style MaxReached fill:#fff3cd
    style Gate1 fill:#fff3cd
    style Gate2 fill:#fff3cd
    style Restart fill:#e1f5ff
```

### Autonomous Agent - SpaceDebrisAgent

```mermaid
graph TD
    Start([Workflow Start: MissionParameters + AgentState]) --> A1[AgentReasoningActivity]
    A1 --> Router{Agent Decision?}
    Router -->|COMPLETE_MISSION| A2[GenerateReportActivity]
    A2 --> End([Return MissionResult])
    Router -->|SCAN_DEBRIS_FIELD| T1[ScanDebrisFieldActivity]
    Router -->|ANALYZE_DEBRIS| T2[AnalyzeDebrisActivity]
    Router -->|MOVE_TO_LOCATION| T3[MoveToLocationActivity]
    Router -->|CHECK_FUEL| T4[CheckFuelActivity]
    Router -->|CAPTURE_DEBRIS| T5[CaptureDebrisActivity]
    Router -->|REQUEST_HUMAN_APPROVAL| T6[RequestHumanApprovalActivity]
    T1 --> Update[UpdateAgentState]
    T2 --> Update
    T3 --> Update
    T4 --> Update
    T5 --> Update
    T6 --> Wait[WaitForExternalEvent: HumanApproval]
    Wait --> Update
    Update --> Check{Failure Conditions?}
    Check -->|Fuel Exhausted| Fail([Mission Aborted])
    Check -->|Max Steps| Fail
    Check -->|Continue| ContinueAsNew[ContinueAsNew: Next Step]
    ContinueAsNew --> A1
    
    style Start fill:#e1f5ff
    style End fill:#d4edda
    style Fail fill:#f8d7da
    style Router fill:#fff3cd
    style Check fill:#fff3cd
    style T1 fill:#cfe2ff
    style T2 fill:#cfe2ff
    style T3 fill:#cfe2ff
    style T4 fill:#cfe2ff
    style T5 fill:#cfe2ff
    style T6 fill:#e7d4ff
    style Wait fill:#e7d4ff
    style ContinueAsNew fill:#fff3cd
```

## Prerequisites

1. Install a container orchestration tool such as Docker Desktop or Podman.
2. Install [.NET 9 SDK](https://dotnet.microsoft.com/download)
3. Install [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/)
4. Install [Ollama](https://ollama.com/)
5. Initialize Dapr: `dapr init`
6. Run Ollama server: `ollama serve` and run a model: `ollama run llama3.2:3b`
7. Run Diagrid Dashboard: `docker run -p 8080:8080 ghcr.io/diagridio/diagrid-dashboard:latest`

See the individual demo README files for specific instructions how to run the applications.

## Next Steps

- Join the [Dapr Discord](https://bit.ly/dapr-discord) to connect with the community.
- Explore the [Diagrid Dev Dashboard](https://www.diagrid.io/blog/improving-the-local-dapr-workflow-experience-diagrid-dashboard) for workflow observability.
- Run your workflows and AI Agents reliably with [Diagrid Catalyst](https://www.diagrid.io/catalyst).
