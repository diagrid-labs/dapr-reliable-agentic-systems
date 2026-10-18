# Space Debris Cleanup - Autonomous Agent Pattern

This demo implements a fully autonomous agent using Dapr Workflow that controls a space debris cleanup mission. The agent makes its own decisions about scanning, analyzing, capturing debris, and requesting human approval for high-risk maneuvers.

## Pattern Overview

The **autonomous agent pattern** implements a self-directed reasoning loop where an LLM decides which actions to take based on current state and observations. This demo features:

1. **Agent Reasoning Loop** - LLM evaluates state and selects next action
2. **Tool Execution** - Agent can invoke tools (scan, move, capture, analyze)
3. **Human-in-the-Loop** - Requests approval for high-risk maneuvers
4. **State Persistence** - ContinueAsNew pattern maintains mission state
5. **Error Recovery** - Adapts to tool failures and unexpected situations

### Key Features

- **Autonomous Decision-Making**: Agent uses LLM to reason and choose actions
- **Tool Usage**: Scan, analyze, navigate, capture debris, check fuel
- **Human-in-the-Loop**: Requests approval for risky maneuvers with timeout
- **Error Recovery**: Adapts to tool failures and unexpected situations
- **State Persistence**: Continues mission across failures using ContinueAsNew
- **External Events**: Workflow waits for human approval via RaiseEventAsync

### Benefits

- ✅ Fully autonomous operation reduces manual intervention
- ✅ Adapts to dynamic environments and unexpected situations
- ✅ Human oversight for critical decisions
- ✅ Resilient to failures with state persistence
- ✅ Extensible tool library for new capabilities

### Drawbacks

- ❌ Unpredictable behavior - agent makes its own choices
- ❌ Higher latency from reasoning loop overhead
- ❌ More expensive due to multiple LLM calls per decision
- ❌ Requires careful prompt engineering for reliable behavior
- ❌ Difficult to debug when agent makes unexpected choices

### When to Use

Use this pattern when tasks require autonomous decision-making in dynamic environments, when the sequence of actions cannot be predetermined, or when you need adaptive behavior based on observations. Ideal for robotic control, dynamic task planning, and interactive systems requiring judgment calls.

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

- [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download)
- [Aspire CLI](https://aspire.dev/get-started/install-cli/) — install with `dotnet tool install -g Aspire.Cli`
- [Docker](https://www.docker.com/products/docker-desktop/) or [Podman](https://podman.io/docs/installation)
- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/) (version 1.18+)
- [Ollama](https://ollama.com/) with the `llama3.2:3b` model pulled

### Start Ollama

```bash
ollama serve
ollama pull llama3.2:3b
```

### Use OpenAI instead of Ollama

The `Resources` folder of the AppHost also contains `conversation-openai.yaml` (model `gpt-4.1-mini`) and `env-secret.yaml` (an environment variable secret store). Both Conversation components are named `conversation`, so keep only one of them in `Resources`. To use OpenAI, remove the Ollama component file and set the API key before starting the app:

```bash
export OPENAI_API_KEY=<your-key>
```

### Run the Application

From the `SpaceDebrisAgent/` folder:

```bash
aspire run
```

This launches the Aspire AppHost, which orchestrates:
- A Valkey container for workflow state persistence (port 16379, password-protected)
- The ApiService with a Dapr sidecar (app ID `space-debris-agent`)
- The Diagrid Dapr Dev Dashboard (`dapr-dev-dashboard` resource) on http://localhost:9090

The Aspire dashboard opens automatically in the browser, showing all resources and their status.

### Test with REST Client

Open `SpaceDebrisAgent.ApiService/SpaceDebrisAgent.ApiService.http` in VS Code and execute the requests to start a cleanup mission. The ApiService HTTP port is shown in the Aspire dashboard.

### Inspect the Workflow runs

Open the Diagrid Dapr Dev Dashboard at `http://localhost:9090` and inspect the workflow executions.

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

`aspire run` starts it as the `dapr-dev-dashboard` resource; open it from the Aspire dashboard or at http://localhost:9090. If it is not installed, only that resource fails to start; the app keeps running.
