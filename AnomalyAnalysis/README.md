# Data's Spatial Anomaly Analysis System

This application implements Data's Spatial Anomaly Analysis System using Dapr Workflow to orchestrate a prompt chaining pattern. The system analyzes spatial anomalies detected by the USS Enterprise-D's sensors through a sequential 5-stage process.

## Pattern Overview

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
cd AnomalyAnalysis
dapr run -f dapr.yaml
```

### Test with REST Client

Open `local.http` in VS Code and execute the requests to analyze anomalies.

### Inspect the Workflow runs

Open the Diagrid Dev Dashboard at `http://localhost:8080` and inspect the workflow executions.

