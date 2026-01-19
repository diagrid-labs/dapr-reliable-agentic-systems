# Space Debris Cleanup - Autonomous Agent Demo

This demo implements a fully autonomous agent using Dapr Workflow that controls a space debris cleanup mission. The agent makes its own decisions about scanning, analyzing, capturing debris, and requesting human approval for high-risk maneuvers.

## Key Features

- **Autonomous Decision-Making**: Agent uses LLM to reason and choose actions
- **Tool Usage**: Scan, analyze, navigate, capture debris, check fuel
- **Human-in-the-Loop**: Requests approval for risky maneuvers with timeout
- **Error Recovery**: Adapts to tool failures and unexpected situations
- **State Persistence**: Continues mission across failures using ContinueAsNew
- **External Events**: Workflow waits for human approval via RaiseEventAsync

## Pattern Overview

## Architecture

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

- **SpaceDebrisCleanupWorkflow**: Main agent loop with ContinueAsNew pattern
- **AgentReasoningActivity**: LLM-based decision making
- **Tool Activities**: Scan, Capture, Move, Analyze, CheckFuel, RequestApproval
- **External Events**: Human approval with 1-minute timeout

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
cd SpaceDebrisAgent
dapr run -f dapr.yaml
```

### Test with REST Client

Open `local.http` in VS Code and execute the requests to start a cleanup mission.

### Inspect the Workflow runs

Open the Diagrid Dev Dashboard at `http://localhost:8080` and inspect the workflow executions.
