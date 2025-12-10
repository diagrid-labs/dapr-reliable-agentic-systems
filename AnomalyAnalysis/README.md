# Data's Spatial Anomaly Analysis System

## Overview
This application implements Data's Spatial Anomaly Analysis System using Dapr Workflow to orchestrate a prompt chaining pattern. The system analyzes spatial anomalies detected by the USS Enterprise-D's sensors through a sequential 5-stage process.

## Prerequisites
1. Install [.NET 9 SDK](https://dotnet.microsoft.com/download)
2. Install [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/)
3. Initialize Dapr: `dapr init`
4. Install Redis (used by Dapr state store)
5. Run Diagrid Dashboard: `docker run -p 8080:8080 ghcr.io/diagridio/diagrid-dashboard:latest`

## Configuration
Update `Resources/conversation.yaml` with your OpenAI API key:
```yaml
- name: key
  value: "your-openai-api-key-here"
```

## Running the Application
```powershell
# Run with Dapr MultiApp Run
dapr run -f .
```

The application will start on http://localhost:5500

## Testing
Open `local.http` in VS Code with the REST Client extension and execute the requests to:
1. Analyze a wormhole anomaly
2. Check workflow status
3. Analyze a quantum singularity (triggers critical alert)
4. Retrieve specific anomaly data

## Architecture
- **Workflow**: `AnomalyAnalysisWorkflow` - Orchestrates 5 sequential stages
- **Activities**: Each stage is an LLM-powered activity using DaprConversationClient
  - ProcessSensorDataActivity
  - ClassifyAnomalyActivity
  - ScientificAnalysisActivity
  - RiskAssessmentActivity
  - GenerateRecommendationActivity
  - AlertBridgeActivity (triggered for critical anomalies)

## Dapr Components
- **State Store**: Redis for workflow state and anomaly data
- **Conversation API**: OpenAI for LLM processing

## Observability
View workflow execution in the Diagrid Dashboard at http://localhost:8080
