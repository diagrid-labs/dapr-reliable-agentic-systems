# Data's Spatial Anomaly Analysis System

This application implements Data's Spatial Anomaly Analysis System using Dapr Workflow to orchestrate a prompt chaining pattern. The system analyzes spatial anomalies detected by the USS Enterprise-D's sensors through a sequential 5-stage process.

## Pattern Overview

The **prompt chaining pattern** orchestrates multiple LLM calls in a sequential pipeline, where each stage's output becomes context for the next stage. This demo implements a 5-stage anomaly analysis workflow:

1. **Process Sensor Data** - Validates and normalizes raw sensor readings
2. **Classify Anomaly** - Categorizes the anomaly type and severity
3. **Scientific Analysis** - Deep dive into the physical phenomena
4. **Risk Assessment** - Evaluates potential threats to ship and crew
5. **Generate Recommendation** - Produces actionable response strategies
6. **Alert Bridge** (conditional) - Immediate notification for critical anomalies

### Benefits

- ✅ Sequential processing builds rich context across stages
- ✅ Specialized prompts optimized for each analysis phase
- ✅ Clear separation of concerns (validation → classification → analysis → recommendations)
- ✅ Easier debugging and monitoring per stage
- ✅ Workflow state management ensures reliability and observability

### Drawbacks

- ❌ Higher latency from sequential processing (no parallelization)
- ❌ Error in early stages blocks later stages
- ❌ Context window limitations with long chains
- ❌ More expensive than single LLM call

### When to Use

Use this pattern when analysis requires multiple specialized stages building on each other's output, and when the problem domain benefits from decomposition into distinct reasoning phases. Ideal for complex analysis, decision trees, and multi-step reasoning tasks.

## Architecture

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

- **Workflow**: `AnomalyAnalysisWorkflow` - Orchestrates 5 sequential stages
- **Activities**: Each stage is an LLM-powered activity using DaprConversationClient
  - ProcessSensorDataActivity
  - ClassifyAnomalyActivity
  - ScientificAnalysisActivity
  - RiskAssessmentActivity
  - GenerateRecommendationActivity
  - AlertBridgeActivity (triggered for critical anomalies)

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

From the `AnomalyAnalysis/` folder:

```bash
aspire run
```

This launches the Aspire AppHost, which orchestrates:
- A Valkey container for workflow state persistence (port 16379, password-protected)
- The ApiService with a Dapr sidecar (app ID `anomaly-detection-app`)
- The Diagrid Dev Dashboard container on http://localhost:18080

The Aspire dashboard opens automatically in the browser, showing all resources and their status.

### Test with REST Client

Open `AnomalyAnalysis.ApiService/AnomalyAnalysis.ApiService.http` in VS Code and execute the requests to analyze anomalies. The ApiService HTTP port is shown in the Aspire dashboard.

### Inspect the Workflow runs

Open the Diagrid Dev Dashboard at `http://localhost:18080` and inspect the workflow executions.

