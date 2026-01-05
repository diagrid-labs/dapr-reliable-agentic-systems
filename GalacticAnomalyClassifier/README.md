# Galactic Anomaly Classifier - PLAN2 Implementation

This project demonstrates **Workflow Routing** using Dapr Workflow to classify and route different types of space anomalies to specialized analysis pipelines.

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

The system uses a two-stage workflow:

1. **Classification Stage**: An LLM-powered classifier determines the anomaly type from sensor data
2. **Routing Stage**: Based on classification, routes to one of five specialized analysis activities:
   - **Temporal Rift** → Quantum Chronodynamics Analysis
   - **Dark Matter Cluster** → Gravitational Physics Analysis
   - **Alien Artifact** → Xenoarchaeology Analysis
   - **Stellar Phenomenon** → Astrophysics Analysis
   - **Dimensional Tear** → Multiverse Theory Analysis

## Project Structure

```
GalacticAnomalyClassifier/
├── Program.cs                              # ASP.NET Core API with endpoints
├── GalacticAnomalyClassifier.csproj       # Project file
├── appsettings.json                        # Configuration
├── dapr.yaml                              # Dapr multi-app configuration
├── local.http                             # REST client test file
├── Models/
│   └── SpaceAnomaly.cs                    # All model classes
├── Workflows/
│   └── AnomalyRoutingWorkflow.cs          # Main routing workflow
├── Activities/
│   ├── ClassifyAnomalyActivity.cs         # LLM classification
│   ├── AnalyzeTemporalRiftActivity.cs     # Temporal analysis
│   ├── AnalyzeDarkMatterActivity.cs       # Dark matter analysis
│   ├── AnalyzeAlienArtifactActivity.cs    # Artifact analysis
│   ├── AnalyzeStellarPhenomenonActivity.cs # Stellar analysis
│   └── AnalyzeDimensionalTearActivity.cs  # Dimensional analysis
└── Resources/
    ├── statestore.yaml                    # State store component
    └── conversation.yaml                  # LLM component (configure API key)
```

## Prerequisites

- .NET 9 SDK
- Dapr CLI (`dapr init`)
- OpenAI API key

## Configuration

1. Update `Resources/conversation.yaml` with your OpenAI API key:
```yaml
metadata:
  - name: key
    value: "YOUR_OPENAI_API_KEY"
```

## Running the Application

From the project directory:

```bash
dapr run -f .
```

The API will be available at `http://localhost:5500`

## Testing

Use the VSCode REST Client extension with `local.http` to test various anomaly types:

1. Temporal Rift - Tests time distortion classification and analysis
2. Dark Matter Cluster - Tests gravitational anomaly detection
3. Alien Artifact - Tests manufactured object identification
4. Stellar Phenomenon - Tests supernova detection
5. Dimensional Tear - Tests reality breach analysis

## API Endpoints

- `POST /anomaly/analyze` - Submit anomaly for classification and analysis
- `GET /anomaly/status/{instanceId}` - Get workflow status and results
- `GET /anomalies/{anomalyId}` - Get specific anomaly data
- `GET /anomalies/stats` - Get classification statistics

## Key Features

- **Intelligent Routing**: LLM-powered classification routes to appropriate specialist
- **Specialized Analysis**: Each anomaly type gets domain-specific analysis
- **Durable Workflows**: Dapr Workflow ensures reliable execution
- **State Management**: Anomaly data and results persisted in state store
- **Observability**: Full workflow tracking and monitoring

## Benefits of Routing Pattern

1. **Specialized Optimization** - Different models and prompts per route
2. **Better Accuracy** - Domain-specific expertise for each category
3. **Cost Efficiency** - Use appropriate model size per complexity
4. **Easy Extension** - Add new anomaly types without affecting existing routes
5. **Clear Metrics** - Track performance per classification type

See [PLAN2.MD](../PLAN2.MD) for complete implementation details and design rationale.
