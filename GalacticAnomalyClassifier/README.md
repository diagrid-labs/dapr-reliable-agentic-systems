# Galactic Anomaly Classifier

This project demonstrates **Workflow Routing** using Dapr Workflow to classify and route different types of space anomalies to specialized analysis pipelines.

## Pattern Overview

The **workflow routing pattern** uses intelligent classification to route requests to specialized handlers. This demo implements a two-stage workflow:

1. **Classification Stage**: An LLM-powered classifier determines the anomaly type from sensor data
2. **Routing Stage**: Based on classification, routes to one of five specialized analysis activities:
   - **Temporal Rift** → Quantum Chronodynamics Analysis
   - **Dark Matter Cluster** → Gravitational Physics Analysis
   - **Alien Artifact** → Xenoarchaeology Analysis
   - **Stellar Phenomenon** → Astrophysics Analysis
   - **Dimensional Tear** → Multiverse Theory Analysis

### Key Features

- **Intelligent Routing**: LLM-powered classification routes to appropriate specialist
- **Specialized Analysis**: Each anomaly type gets domain-specific analysis
- **Durable Workflows**: Dapr Workflow ensures reliable execution
- **State Management**: Anomaly data and results persisted in state store
- **Observability**: Full workflow tracking and monitoring

### Benefits

- ✅ Specialized optimization - Different models and prompts per route
- ✅ Better accuracy - Domain-specific expertise for each category
- ✅ Cost efficiency - Use appropriate model size per complexity
- ✅ Easy extension - Add new anomaly types without affecting existing routes
- ✅ Clear metrics - Track performance per classification type

### Drawbacks

- ❌ Additional latency from classification step
- ❌ Misclassification can route to wrong specialist
- ❌ More complex to maintain multiple specialized handlers
- ❌ Requires training data or examples for accurate classification

### When to Use

Use this pattern when you have distinct categories requiring specialized handling, when different routes benefit from different models/prompts, or when you need to optimize cost/latency per category. Ideal for multi-domain systems, tiered support routing, and specialized expert consultation.

## Architecture

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

From the `GalacticAnomalyClassifier/` folder:

```bash
aspire run
```

This launches the Aspire AppHost, which orchestrates:
- A Valkey container for workflow state persistence (port 16379, password-protected)
- The ApiService with a Dapr sidecar (app ID `anomaly-routing-app`)
- The Diagrid Dev Dashboard container on http://localhost:18080

The Aspire dashboard opens automatically in the browser, showing all resources and their status.

### Test with REST Client

Open `GalacticAnomalyClassifier.ApiService/GalacticAnomalyClassifier.ApiService.http` in VS Code and execute the requests to classify different anomalies. The ApiService HTTP port is shown in the Aspire dashboard.

### Inspect the Workflow runs

Open the Diagrid Dev Dashboard at `http://localhost:18080` and inspect the workflow executions.

