# Plan 1: Workflow Prompt Chaining - Data's Spatial Anomaly Analysis System

## Overview
This demo implements a **prompt chaining workflow** using Dapr Workflow to analyze spatial anomalies detected by the USS Enterprise-D's sensors. Lieutenant Commander Data has designed this system to decompose the complex task of identifying, classifying, analyzing, and recommending actions for spatial anomalies into a sequential chain of LLM-powered steps, ensuring Captain Picard has complete scientific data before making command decisions.

## Sci-Fi Use Case: Lieutenant Commander Data's Spatial Anomaly Analysis System
You're building a backend system for Data to analyze spatial anomalies detected by the USS Enterprise-D's long-range sensors. Lieutenant Commander Data, with his positronic brain and comprehensive scientific databases, has designed a multi-stage analysis system that ensures all anomalies are thoroughly understood before Captain Picard makes navigation or tactical decisions:
1. **Sensor Data Processing** - Data processes raw sensor readings from long-range scanners and converts them to structured scientific data
2. **Anomaly Classification** - Classify the anomaly type (wormhole, subspace rift, quantum singularity, temporal distortion, etc.)
3. **Scientific Analysis** - Deep analysis of the anomaly's properties, behavior patterns, and scientific significance
4. **Risk Assessment** - Geordi La Forge and Worf evaluate potential dangers to the Enterprise (radiation, gravitational stress, hostile entities)
5. **Recommendation Generation** - Generate tactical recommendations for Captain Picard (investigate, avoid, report to Starfleet Science)

## Architecture

### Services
1. **ScienceStationAPI** - ASP.NET Core Web API (Entry point - Science Station)
2. **AnomalyAnalysisWorkflow** - Dapr Workflow orchestrator (Data's analysis protocols)
3. **WorkflowActivities** - Individual LLM-powered activities for each analysis step

### Components Used
- **Dapr Workflow** - Orchestrates the prompt chaining sequence
- **Dapr State Management** - Stores intermediate results and signal history
- **Dapr Conversation API** - Powers each LLM-based processing step

## Detailed Implementation Plan

### Step 1: Project Setup
```
/AnomalyAnalysis
  - AnomalyAnalysis.csproj
  - Program.cs
  - local.http
  - appsettings.json
  - dapr.yaml
  /Workflows
    - AnomalyAnalysisWorkflow.cs
  /Activities
    - ProcessSensorDataActivity.cs
    - ClassifyAnomalyActivity.cs
    - ScientificAnalysisActivity.cs
    - RiskAssessmentActivity.cs
    - GenerateRecommendationActivity.cs
  /Models
    - SpatialAnomaly.cs
    - AnalysisResult.cs
    - AnomalyMetadata.cs
  /Resources
    - statestore.yaml
    - conversation.yaml
  /Properties
    - launchSettings.json
```

### Step 2: Core Models
**SpatialAnomaly.cs**
```csharp
public record SpatialAnomaly(
    string AnomalyId,
    string RawSensorData,
    string SpatialCoordinates,
    DateTime DetectedAt,
    Dictionary<string, double> SensorReadings
);

public record AnalysisStage(
    string StageName,
    string Input,
    string Output,
    bool Success,
    string? ErrorMessage = null
);

public record AnalysisResult(
    string AnomalyId,
    List<AnalysisStage> Stages,
    string AnomalyType,
    string ScientificAnalysis,
    string RiskLevel,
    string TacticalRecommendation
);
```

### Step 3: Workflow Definition
**AnomalyAnalysisWorkflow.cs**
```csharp
public class AnomalyAnalysisWorkflow : Workflow<SpatialAnomaly, AnalysisResult>
{
    public override async Task<AnalysisResult> RunAsync(
        WorkflowContext context, 
        SpatialAnomaly input)
    {
        var stages = new List<AnalysisStage>();
        
        // Stage 1: Process Sensor Data
        var processedData = await context.CallActivityAsync<string>(
            nameof(ProcessSensorDataActivity),
            input.RawSensorData);
        
        stages.Add(new AnalysisStage(
            "ProcessSensorData", 
            input.RawSensorData, 
            processedData, 
            true));
        
        // Gate check: Ensure sensor data processing was successful
        if (string.IsNullOrEmpty(processedData))
        {
            return FailedResult(input.AnomalyId, stages, "Sensor data processing failed");
        }
        
        // Stage 2: Classify Anomaly
        var anomalyType = await context.CallActivityAsync<string>(
            nameof(ClassifyAnomalyActivity),
            processedData);
        
        stages.Add(new AnalysisStage(
            "ClassifyAnomaly", 
            processedData, 
            anomalyType, 
            true));
        
        // Stage 3: Scientific Analysis
        var scientificAnalysis = await context.CallActivityAsync<string>(
            nameof(ScientificAnalysisActivity),
            new { ProcessedData = processedData, AnomalyType = anomalyType });
        
        stages.Add(new AnalysisStage(
            "ScientificAnalysis", 
            anomalyType, 
            scientificAnalysis, 
            true));
        
        // Stage 4: Risk Assessment
        var riskLevel = await context.CallActivityAsync<string>(
            nameof(RiskAssessmentActivity),
            new { AnomalyType = anomalyType, Analysis = scientificAnalysis });
        
        stages.Add(new AnalysisStage(
            "RiskAssessment", 
            scientificAnalysis, 
            riskLevel, 
            true));
        
        // Gate check: Alert bridge if critical risk detected
        if (riskLevel == "CRITICAL")
        {
            await context.CallActivityAsync(
                nameof(AlertBridgeActivity),
                input.AnomalyId);
        }
        
        // Stage 5: Generate Tactical Recommendation
        var recommendation = await context.CallActivityAsync<string>(
            nameof(GenerateRecommendationActivity),
            new { 
                AnomalyType = anomalyType, 
                Analysis = scientificAnalysis,
                RiskLevel = riskLevel 
            });
        
        stages.Add(new AnalysisStage(
            "GenerateRecommendation", 
            riskLevel, 
            recommendation, 
            true));
        
        return new AnalysisResult(
            input.AnomalyId,
            stages,
            anomalyType,
            scientificAnalysis,
            riskLevel,
            recommendation
        );
    }
}
```

### Step 4: Activity Implementations
**ProcessSensorDataActivity.cs**
```csharp
using Dapr.AI.Conversation;
using Dapr.AI.Conversation.Extensions;

public class ProcessSensorDataActivity : WorkflowActivity<string, string>
{
    private readonly DaprConversationClient _conversationClient;
    
    public ProcessSensorDataActivity(DaprConversationClient conversationClient)
    {
        _conversationClient = conversationClient;
    }
    
    public override async Task<string> RunAsync(
        WorkflowActivityContext context, 
        string rawData)
    {
        var conversationOptions = new ConversationOptions("conversation")
        {
            Temperature = 0.7,
            Model = "gpt-4o"
        };
        
        var response = await _conversationClient.ConverseAsync(
            [
                new ConversationInput(new List<IConversationMessage>
                {
                    new SystemMessage
                    {
                        Content = [
                            new MessageContent(@"You are Lt. Commander Data's sensor analysis subroutine. 
                            Process raw sensor data from the Enterprise's long-range scanners. 
                            Convert electromagnetic readings, subspace distortions, and quantum 
                            fluctuations into structured scientific data with key measurements 
                            (wavelength, frequency, intensity, spatial coordinates).")
                        ]
                    },
                    new UserMessage
                    {
                        Name = "DataAnalysis",
                        Content = [
                            new MessageContent($"Process sensor data: {rawData}")
                        ]
                    }
                })
            ],
            conversationOptions);
        
        return response.Outputs.First().Choices.First().Message.Content;
    }
}
```

**ClassifyAnomalyActivity.cs**
```csharp
using Dapr.AI.Conversation;
using Dapr.AI.Conversation.Extensions;

public class ClassifyAnomalyActivity : WorkflowActivity<string, string>
{
    private readonly DaprConversationClient _conversationClient;
    
    public ClassifyAnomalyActivity(DaprConversationClient conversationClient)
    {
        _conversationClient = conversationClient;
    }
    
    public override async Task<string> RunAsync(
        WorkflowActivityContext context, 
        string processedData)
    {
        var conversationOptions = new ConversationOptions("conversation")
        {
            Temperature = 0.3,
            Model = "gpt-4o"
        };
        
        var response = await _conversationClient.ConverseAsync(
            [
                new ConversationInput(new List<IConversationMessage>
                {
                    new SystemMessage
                    {
                        Content = [
                            new MessageContent(@"You are Data's scientific classification system. 
                            Classify spatial anomalies based on sensor data. Categories include: 
                            WORMHOLE, SUBSPACE_RIFT, QUANTUM_SINGULARITY, TEMPORAL_DISTORTION, 
                            DARK_MATTER_CLOUD, STELLAR_NURSERY, NEBULA, GRAVIMETRIC_DISTORTION. 
                            Return only the classification type.")
                        ]
                    },
                    new UserMessage
                    {
                        Name = "DataClassification",
                        Content = [
                            new MessageContent($"Classify this anomaly: {processedData}")
                        ]
                    }
                })
            ],
            conversationOptions);
        
        return response.Outputs.First().Choices.First().Message.Content.Trim();
    }
}
```

**ScientificAnalysisActivity.cs**
```csharp
using Dapr.AI.Conversation;
using Dapr.AI.Conversation.Extensions;

public class ScientificAnalysisActivity : WorkflowActivity<object, string>
{
    private readonly DaprConversationClient _conversationClient;
    
    public ScientificAnalysisActivity(DaprConversationClient conversationClient)
    {
        _conversationClient = conversationClient;
    }
    
    public override async Task<string> RunAsync(
        WorkflowActivityContext context, 
        object input)
    {
        var data = (dynamic)input;
        string processedData = data.ProcessedData;
        string anomalyType = data.AnomalyType;
        
        var conversationOptions = new ConversationOptions("conversation")
        {
            Temperature = 0.7,
            Model = "gpt-4o"
        };
        
        var response = await _conversationClient.ConverseAsync(
            [
                new ConversationInput(new List<IConversationMessage>
                {
                    new SystemMessage
                    {
                        Content = [
                            new MessageContent(@"You are Data's scientific analysis system. Provide detailed 
                            analysis of spatial anomalies including: formation theories, energy 
                            signatures, spatial dimensions, stability factors, scientific significance, 
                            and potential for research. Use appropriate astrophysics terminology.")
                        ]
                    },
                    new UserMessage
                    {
                        Name = "DataScience",
                        Content = [
                            new MessageContent($"Analyze {anomalyType}: {processedData}")
                        ]
                    }
                })
            ],
            conversationOptions);
        
        return response.Outputs.First().Choices.First().Message.Content;
    }
}
```

**RiskAssessmentActivity.cs**
```csharp
using Dapr.AI.Conversation;
using Dapr.AI.Conversation.Extensions;

public class RiskAssessmentActivity : WorkflowActivity<object, string>
{
    private readonly DaprConversationClient _conversationClient;
    
    public RiskAssessmentActivity(DaprConversationClient conversationClient)
    {
        _conversationClient = conversationClient;
    }
    
    public override async Task<string> RunAsync(
        WorkflowActivityContext context, 
        object input)
    {
        var data = (dynamic)input;
        string anomalyType = data.AnomalyType;
        string analysis = data.Analysis;
        
        var conversationOptions = new ConversationOptions("conversation")
        {
            Temperature = 0.3,
            Model = "gpt-4o"
        };
        
        var response = await _conversationClient.ConverseAsync(
            [
                new ConversationInput(new List<IConversationMessage>
                {
                    new SystemMessage
                    {
                        Content = [
                            new MessageContent(@"You are Geordi La Forge and Worf's combined risk assessment system. 
                            Evaluate spatial anomalies for danger to the Enterprise: 
                            - LOW: No immediate danger, safe to approach
                            - MODERATE: Potential risks, caution advised
                            - HIGH: Significant danger, shields and defensive posture required
                            - CRITICAL: Immediate threat to ship integrity or crew safety
                            
                            Consider: radiation levels, gravitational stress on hull, subspace interference, 
                            temporal effects. Return only the risk level.")
                        ]
                    },
                    new UserMessage
                    {
                        Name = "GeordiAndWorf",
                        Content = [
                            new MessageContent($"Assess risk for {anomalyType}:\n{analysis}")
                        ]
                    }
                })
            ],
            conversationOptions);
        
        return response.Outputs.First().Choices.First().Message.Content.Trim();
    }
}
```

**GenerateRecommendationActivity.cs**
```csharp
using Dapr.AI.Conversation;
using Dapr.AI.Conversation.Extensions;

public class GenerateRecommendationActivity : WorkflowActivity<object, string>
{
    private readonly DaprConversationClient _conversationClient;
    
    public GenerateRecommendationActivity(DaprConversationClient conversationClient)
    {
        _conversationClient = conversationClient;
    }
    
    public override async Task<string> RunAsync(
        WorkflowActivityContext context, 
        object input)
    {
        var data = (dynamic)input;
        string anomalyType = data.AnomalyType;
        string analysis = data.Analysis;
        string riskLevel = data.RiskLevel;
        
        var conversationOptions = new ConversationOptions("conversation")
        {
            Temperature = 0.7,
            Model = "gpt-4o"
        };
        
        var response = await _conversationClient.ConverseAsync(
            [
                new ConversationInput(new List<IConversationMessage>
                {
                    new SystemMessage
                    {
                        Content = [
                            new MessageContent(@"You are Data's tactical recommendation system for Captain Picard. 
                            Generate clear, actionable recommendations for spatial anomalies:
                            - INVESTIGATE: Deploy shuttlecraft with sensor package, maintain safe distance
                            - STUDY_FROM_DISTANCE: Use long-range sensors, collect data without approach
                            - AVOID: Set course around anomaly, log coordinates for Starfleet Science
                            - EMERGENCY_EVASION: Immediate warp speed departure, shields to maximum
                            - REPORT_TO_STARFLEET: Notify Starfleet Command and Science Division
                            
                            Include specific tactical details for implementation.")
                        ]
                    },
                    new UserMessage
                    {
                        Name = "DataTactical",
                        Content = [
                            new MessageContent($"Generate recommendation for {anomalyType} (Risk: {riskLevel}):\n{analysis}")
                        ]
                    }
                })
            ],
            conversationOptions);
        
        return response.Outputs.First().Choices.First().Message.Content;
    }
}
```

### Step 5: API Endpoints
**Program.cs**
```csharp
using Dapr.Client;
using Dapr.Workflow;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDaprClient();
builder.Services.AddDaprConversationClient();
builder.Services.AddDaprWorkflow(options =>
{
    options.RegisterWorkflow<AnomalyAnalysisWorkflow>();
    options.RegisterActivity<ProcessSensorDataActivity>();
    options.RegisterActivity<ClassifyAnomalyActivity>();
    options.RegisterActivity<ScientificAnalysisActivity>();
    options.RegisterActivity<RiskAssessmentActivity>();
    options.RegisterActivity<GenerateRecommendationActivity>();
    options.RegisterActivity<AlertBridgeActivity>();
});

var app = builder.Build();

// Start analyzing a spatial anomaly
app.MapPost("/anomaly/analyze", async (
    SpatialAnomaly anomaly,
    DaprWorkflowClient workflowClient,
    DaprClient daprClient) =>
{
    var instanceId = $"anomaly-{anomaly.AnomalyId}";
    
    // Store original anomaly data
    await daprClient.SaveStateAsync(
        "statestore", 
        $"anomaly-{anomaly.AnomalyId}", 
        anomaly);
    
    // Start workflow
    await workflowClient.ScheduleNewWorkflowAsync(
        nameof(AnomalyAnalysisWorkflow),
        instanceId,
        anomaly);
    
    return Results.Accepted($"/anomaly/status/{instanceId}", new { instanceId });
});

// Get analysis status
app.MapGet("/anomaly/status/{instanceId}", async (
    string instanceId,
    DaprWorkflowClient workflowClient) =>
{
    var state = await workflowClient.GetWorkflowStateAsync(instanceId);
    
    if (state == null)
        return Results.NotFound();
    
    return Results.Ok(new
    {
        instanceId,
        status = state.RuntimeStatus.ToString(),
        result = state.ReadOutputAs<AnalysisResult>(),
        createdAt = state.CreatedAt,
        lastUpdatedAt = state.LastUpdatedAt
    });
});

// Get a specific analyzed anomaly by ID
app.MapGet("/anomalies/{anomalyId}", async (
    string anomalyId,
    DaprClient daprClient) =>
{
    var anomaly = await daprClient.GetStateAsync<SpatialAnomaly>(
        "statestore",
        anomalyId);
    
    if (anomaly == null)
        return Results.NotFound();
    
    return Results.Ok(anomaly);
});

app.Run();
```

### Step 6: Dapr MultiApp Run
**dapr.yaml**
```yaml
version: 1
common:
  resourcesPath: Resources
  appLogDestination: fileAndConsole
  daprdLogDestination: fileAndConsole
apps:
  - appID: anomaly-detection-app
    appDirPath: /
    appPort: 5500
    daprHTTPPort: 3550
    command: ["dotnet", "run"]
```

### Step 7: Local HTTP Tests
**local.http**
```http
### Analyze a wormhole anomaly
POST http://localhost:5500/anomaly/analyze
Content-Type: application/json

{
  "anomalyId": "ANM-001-WORMHOLE",
  "rawSensorData": "SUBSPACE_SIGNATURE: 47.3 THz | GRAVIMETRIC_DISTORTION: 8.2 tesla | TEMPORAL_VARIANCE: 0.003% | SPATIAL_COORDINATES: Sector 001 | QUANTUM_FLUCTUATION: High | BARYON_SWEEP: Negative",
  "spatialCoordinates": "Sector 001, Alpha Quadrant",
  "detectedAt": "2370-06-15T14:32:00Z",
  "sensorReadings": {
    "subspaceFrequency": 47.3,
    "gravimetricDistortion": 8.2,
    "temporalVariance": 0.003,
    "radiationLevel": 2.1
  }
}

### Get analysis status
GET http://localhost:5500/anomaly/status/anomaly-ANM-001-WORMHOLE

### Analyze a quantum singularity (high risk)
POST http://localhost:5500/anomaly/analyze
Content-Type: application/json

{
  "anomalyId": "ANM-002-SINGULARITY",
  "rawSensorData": "QUANTUM_SINGULARITY_DETECTED | GRAVIMETRIC_DISTORTION: 94.7 tesla | EVENT_HORIZON: Expanding | HAWKING_RADIATION: 15.2 TeV | SPATIAL_TEAR: Forming | WARNING: EXTREME_GRAVITY",
  "spatialCoordinates": "Sector 441, Neutral Zone",
  "detectedAt": "2370-06-15T16:45:00Z",
  "sensorReadings": {
    "gravimetricDistortion": 94.7,
    "hawkingRadiation": 15.2,
    "eventHorizonRadius": 342.0,
    "spatialStress": 8.9
  }
}

### Get all anomaly history
GET http://localhost:5500/anomalies/history
```

### Step 8: Dapr Components
**Resources/statestore.yaml**
```yaml
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: statestore
spec:
  type: state.redis
  version: v1
  metadata:
  - name: redisHost
    value: localhost:6379
  - name: redisPassword
    value: ""
  - name: actorStateStore
    value: "true"
```

**components/conversation.yaml**
```yaml
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: conversation
spec:
  type: conversation.openai
  version: v1
  metadata:
  - name: key
    value: "your-openai-api-key"
  - name: endpoint
    value: "https://api.openai.com/v1"
```

## Benefits of Prompt Chaining Pattern

### 1. **Higher Accuracy Through Decomposition**
Each LLM call focuses on a single, well-defined task (decode, translate, assess, generate, validate). This specialization leads to better results than asking one LLM to do everything at once.

### 2. **Transparency and Debuggability**
Each stage's input/output is captured in the `ProcessingStage` list, making it easy to:
- Identify which step failed
- Understand the transformation at each stage
- Debug issues in production
- Audit decisions for compliance

### 3. **Gate Checks for Control Flow**
The workflow includes programmatic validation points:
- Verify decoding succeeded before translation
- Auto-escalate critical threats to fleet command
- Retry response generation if protocol validation fails

This prevents garbage data from propagating through the chain.

### 4. **Predictable Latency**
The number of steps is fixed and known ahead of time, making it easy to:
- Set accurate SLAs
- Budget for LLM costs
- Optimize specific slow stages

### 5. **Easy to Test and Optimize**
Each activity can be:
- Unit tested independently
- Optimized with different models (cheap/fast for simple tasks, powerful for complex ones)
- Replaced with mock implementations for testing

### 6. **Dapr Workflow Benefits**
- **Automatic retries** - Activities retry on transient failures
- **State persistence** - Workflow state survives crashes
- **Replay safety** - Deterministic execution even after interruptions
- **Observability** - Built-in tracking of workflow progress

## Drawbacks of Prompt Chaining Pattern

### 1. **Sequential Latency**
Each step waits for the previous one to complete. For our 5-stage workflow:
- If each LLM call takes 2 seconds, total latency is 10+ seconds
- Cannot be parallelized (output of stage N is input to stage N+1)
- User must wait for entire chain to complete

### 2. **Error Propagation**
Errors in early stages cascade:
- A bad translation affects threat assessment
- Incorrect threat level leads to inappropriate response
- The entire result may be unusable if stage 1 fails

### 3. **Cost Accumulation**
Every signal goes through all 5 LLM calls, even simple ones that might not need full processing. For example:
- A routine "navigation malfunction" signal still gets full threat assessment
- Low-threat signals still require response generation and validation
- No way to short-circuit for trivial cases

### 4. **Rigid Structure**
The chain is fixed at design time:
- Can't skip steps based on signal type
- Can't dynamically add steps for complex cases
- All signals follow the same path regardless of complexity

### 5. **Prompt Engineering Overhead**
Each activity needs carefully crafted prompts:
- Must ensure output format matches next stage's input expectations
- Changes to one stage may require updating adjacent stages
- Maintaining consistency across 5+ prompts is complex

### 6. **Intermediate State Storage**
Dapr Workflow stores state for each activity, which:
- Increases storage costs for high-volume scenarios
- Adds I/O overhead for state persistence
- Requires cleanup/archival strategy for completed workflows

## When to Use This Pattern

**Use prompt chaining when:**
- Task has clear, sequential stages
- Each stage needs full LLM reasoning
- Output quality is more important than speed
- Auditability and transparency are required
- Error recovery needs to happen at specific checkpoints

**Avoid prompt chaining when:**
- Need low latency (consider parallelization instead)
- Steps can be independent (use parallel workflow)
- Task complexity varies widely (use routing or orchestrator-worker instead)
- Simple cases dominate (optimize for the common path first)

## Running the Demo

### Prerequisites
```powershell
# Install Dapr CLI
powershell -Command "iwr -useb https://raw.githubusercontent.com/dapr/cli/master/install/install.ps1 | iex"

# Initialize Dapr
dapr init

# Run the Diagrid Dashboard
docker run -p 8080:8080 ghcr.io/diagridio/diagrid-dashboard:latest
```

### Run the application
```powershell
# Run with Dapr MultiApp Run
dapr run -f .
```

### Test the workflow
Use the VSCode REST Client extension with `local.http` to analyze anomalies and check status.

## Conclusion
This prompt chaining workflow demonstrates how Dapr Workflow can orchestrate multi-stage LLM processing with built-in reliability, observability, and state management. Lieutenant Commander Data's sequential analysis system ensures each spatial anomaly is thoroughly understood—from raw sensor data to tactical recommendations for Captain Picard—though at the cost of higher latency compared to parallel approaches. The systematic approach reflects Data's methodical scientific analysis style.
