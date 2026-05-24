# StarshipDiagnostics - Parallel Workflow Pattern Demo

This demo implements the **parallelization workflow pattern** using Dapr Workflow to perform comprehensive diagnostics on starships like the USS Enterprise-D. The system runs multiple independent scans simultaneously (sectioning) across all major ship systems and uses voting mechanisms to assess critical findings.

## Pattern Overview

The **parallelization pattern** breaks work into independent parallel tasks for faster execution and uses voting mechanisms for consensus on critical decisions. This demo showcases:

1. **Sectioning** - Breaking work into independent parallel tasks
2. **Voting** - Using multiple AI models for consensus on critical decisions
3. **Aggregation** - Combining parallel results into a unified output

### Key Features

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

### Benefits

- ✅ Parallel execution dramatically reduces total scan time
- ✅ Independent scans prevent cross-contamination of results
- ✅ Voting ensures robust decisions on critical findings
- ✅ Easy to add new scan types or voters
- ✅ Workflow orchestration provides reliability and observability

### Drawbacks

- ❌ Resource intensive - multiple LLM calls running concurrently
- ❌ More expensive than sequential processing
- ❌ Requires orchestration overhead
- ❌ Voting can increase latency for critical findings

### When to Use

Use this pattern when tasks are independent and can run in parallel, when you need consensus on critical decisions, or when reducing total execution time is more important than cost. Ideal for comprehensive diagnostics, multi-perspective analysis, and distributed validation.

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

### Implementation Details

- **Dapr Workflow**: Orchestrates parallel execution and voting
- **Dapr State Management**: Stores scan results and maintenance history
- **Dapr Conversation API**: Powers each diagnostic scan and vote using LLMs

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

From the `StarshipDiagnostics/` folder:

```bash
aspire run
```

This launches the Aspire AppHost, which orchestrates:
- A Valkey container for workflow state persistence (port 16379, password-protected)
- The ApiService with a Dapr sidecar (app ID `starship-diagnostics-app`)
- The Diagrid Dev Dashboard container on http://localhost:18080

The Aspire dashboard opens automatically in the browser, showing all resources and their status.

### Test with REST Client

Open `StarshipDiagnostics.ApiService/StarshipDiagnostics.ApiService.http` in VS Code and execute the requests to start a diagnostic scan. The ApiService HTTP port is shown in the Aspire dashboard.

### Inspect the Workflow runs

Open the Diagrid Dev Dashboard at `http://localhost:18080` and inspect the workflow executions.

