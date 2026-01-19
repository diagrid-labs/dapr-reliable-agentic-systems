# StarshipDiagnostics - Parallel Workflow Pattern Demo

This demo implements the **parallelization workflow pattern** using Dapr Workflow to perform comprehensive diagnostics on starships like the USS Enterprise-D. The system runs multiple independent scans simultaneously (sectioning) across all major ship systems and uses voting mechanisms to assess critical findings.

## Pattern Overview

This demo showcases:

1. **Sectioning** - Breaking work into independent parallel tasks
2. **Voting** - Using multiple AI models for consensus on critical decisions
3. **Aggregation** - Combining parallel results into a unified output

## Architecture

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

- **Dapr Workflow**: Orchestrates parallel execution and voting
- **Dapr State Management**: Stores scan results and maintenance history
- **Dapr Conversation API**: Powers each diagnostic scan and vote using LLMs


## Features

- **Parallel Scanner Activities**: Five independent diagnostic scans run simultaneously
  - Hull Integrity Scanner
  - Reactor Core Scanner
  - Navigation Systems Scanner
  - Life Support Scanner
  - Tactical Systems Scanner

- **Voting Pattern**: Critical findings trigger parallel evaluation by three AI voters
  - Safety Voter (crew safety perspective)
  - Severity Voter (technical severity assessment)
  - Recommendation Voter (cost-benefit analysis)

- **Result Aggregation**: Combines all scan results and votes into a comprehensive diagnostic report

## Running the Demo

### Prerequisites

- Docker Desktop or Podman
- .NET 9 SDK
- Dapr CLI
- Ollama

### Start Ollama

```bash
ollama serve
ollama run llama3.2:3b
```

### Start the Diagrid Dev Dashboard

```bash
docker run -p 8080:8080 ghcr.io/diagridio/diagrid-dashboard:latest
```

### Run the Application

```bash
cd StarshipDiagnostics
dapr run -f dapr.yaml
```

### Test with REST Client

Open `local.http` in VS Code and execute the requests to start a diagnostic scan.

### Inspect the Workflow runs

Open the Diagrid Dev Dashboard at `http://localhost:8080` and inspect the workflow executions.

