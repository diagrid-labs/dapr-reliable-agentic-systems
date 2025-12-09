# Plan 2: Workflow Routing - Galactic Anomaly Classification System

## Overview
This demo implements a **routing workflow** using Dapr Workflow to classify and route different types of anomalies detected by the USS Enterprise-D's sensors. Geordi La Forge's diagnostic system uses an LLM classifier to determine anomaly type, then routes to the appropriate crew specialist (Data, Geordi, Dr. Crusher, or Captain Picard) for analysis.

## Sci-Fi Use Case: Geordi La Forge's Engineering Diagnostic Routing System
You're building a backend system for Chief Engineer Geordi La Forge to diagnose and route various technical anomalies detected by the Enterprise's sensor arrays. Geordi has designed a classification system that routes each anomaly type to the appropriate specialist:
1. **Warp Core Anomalies** - Routed to Engineering (Geordi La Forge)
2. **Subspace Distortions** - Routed to Science (Data)
3. **Unknown Artifacts** - Routed to Archaeology Analysis (Captain Picard)
4. **Stellar Phenomena** - Routed to Astrophysics (Data)
5. **Biological Unknowns** - Routed to Medical (Dr. Beverly Crusher)

A single generic analysis pipeline would be ineffective. Each type needs specialized expertise, tools, and analysis protocols from different Enterprise crew members.

## Architecture

### Services
1. **AnomalyAPI** - ASP.NET Core Web API (Entry point)
2. **AnomalyRoutingWorkflow** - Dapr Workflow orchestrator with routing
3. **SpecializedActivities** - Type-specific analysis activities

### Components Used
- **Dapr Workflow** - Orchestrates routing logic
- **Dapr State Management** - Stores anomaly data and analysis results
- **Dapr Conversation API** - Powers classification and specialized analysis

## Detailed Implementation Plan

### Step 1: Project Setup
```
/GalacticAnomalyClassifier
  - Program.cs
  - local.http
  - appsettings.json
  - dapr.yaml
  /Workflows
    - AnomalyRoutingWorkflow.cs
  /Activities
    - ClassifyAnomalyActivity.cs
    - AnalyzeTemporalRiftActivity.cs
    - AnalyzeDarkMatterActivity.cs
    - AnalyzeAlienArtifactActivity.cs
    - AnalyzeStellarPhenomenonActivity.cs
    - AnalyzeDimensionalTearActivity.cs
  /Models
    - SpaceAnomaly.cs
    - AnomalyClassification.cs
    - AnalysisResult.cs
  /Resources
    - statestore.yaml
    - conversation.yaml
```

### Step 2: Core Models
**SpaceAnomaly.cs**
```csharp
public record SpaceAnomaly(
    string AnomalyId,
    string SensorData,
    string Coordinates,
    DateTime DetectedAt,
    Dictionary<string, double> Measurements
);

public record AnomalyClassification(
    string Type,
    double Confidence,
    string Reasoning
);

public enum AnomalyType
{
    TemporalRift,
    DarkMatterCluster,
    AlienArtifact,
    StellarPhenomenon,
    DimensionalTear,
    Unknown
}

public record AnalysisResult(
    string AnomalyId,
    AnomalyType Type,
    string Classification,
    string SpecializedAnalysis,
    Dictionary<string, object> Metrics,
    List<string> Recommendations,
    string ThreatLevel
);
```

### Step 3: Workflow Definition with Routing
**AnomalyRoutingWorkflow.cs**
```csharp
public class AnomalyRoutingWorkflow : Workflow<SpaceAnomaly, AnalysisResult>
{
    public override async Task<AnalysisResult> RunAsync(
        WorkflowContext context, 
        SpaceAnomaly input)
    {
        // Stage 1: Classify the anomaly type
        var classification = await context.CallActivityAsync<AnomalyClassification>(
            nameof(ClassifyAnomalyActivity),
            input);
        
        // Route based on classification
        var anomalyType = ParseAnomalyType(classification.Type);
        
        // Stage 2: Route to specialized analysis
        string specializedAnalysis;
        Dictionary<string, object> metrics;
        List<string> recommendations;
        string threatLevel;
        
        switch (anomalyType)
        {
            case AnomalyType.TemporalRift:
                var temporalResult = await context.CallActivityAsync<TemporalAnalysis>(
                    nameof(AnalyzeTemporalRiftActivity),
                    input);
                specializedAnalysis = temporalResult.Analysis;
                metrics = temporalResult.QuantumMetrics;
                recommendations = temporalResult.SafetyProtocols;
                threatLevel = temporalResult.TimelineStability;
                break;
                
            case AnomalyType.DarkMatterCluster:
                var darkMatterResult = await context.CallActivityAsync<DarkMatterAnalysis>(
                    nameof(AnalyzeDarkMatterActivity),
                    input);
                specializedAnalysis = darkMatterResult.Analysis;
                metrics = darkMatterResult.GravitationalData;
                recommendations = darkMatterResult.HarvestingOpportunities;
                threatLevel = darkMatterResult.CollapseProbability;
                break;
                
            case AnomalyType.AlienArtifact:
                var artifactResult = await context.CallActivityAsync<ArtifactAnalysis>(
                    nameof(AnalyzeAlienArtifactActivity),
                    input);
                specializedAnalysis = artifactResult.Analysis;
                metrics = artifactResult.XenoarchaeologyData;
                recommendations = artifactResult.ExtractionProcedures;
                threatLevel = artifactResult.HostilityIndicators;
                break;
                
            case AnomalyType.StellarPhenomenon:
                var stellarResult = await context.CallActivityAsync<StellarAnalysis>(
                    nameof(AnalyzeStellarPhenomenonActivity),
                    input);
                specializedAnalysis = stellarResult.Analysis;
                metrics = stellarResult.AstrophysicsData;
                recommendations = stellarResult.ObservationProtocols;
                threatLevel = stellarResult.RadiationLevel;
                break;
                
            case AnomalyType.DimensionalTear:
                var dimensionalResult = await context.CallActivityAsync<DimensionalAnalysis>(
                    nameof(AnalyzeDimensionalTearActivity),
                    input);
                specializedAnalysis = dimensionalResult.Analysis;
                metrics = dimensionalResult.MultiverseMetrics;
                recommendations = dimensionalResult.ContainmentProcedures;
                threatLevel = dimensionalResult.RealityStability;
                break;
                
            default:
                specializedAnalysis = "Unknown anomaly type - general observation recommended";
                metrics = new Dictionary<string, object>();
                recommendations = new List<string> { "Maintain safe distance", "Continue monitoring" };
                threatLevel = "UNKNOWN";
                break;
        }
        
        return new AnalysisResult(
            input.AnomalyId,
            anomalyType,
            classification.Type,
            specializedAnalysis,
            metrics,
            recommendations,
            threatLevel
        );
    }
    
    private AnomalyType ParseAnomalyType(string type)
    {
        return type.ToLower() switch
        {
            var t when t.Contains("temporal") || t.Contains("time") => AnomalyType.TemporalRift,
            var t when t.Contains("dark matter") => AnomalyType.DarkMatterCluster,
            var t when t.Contains("artifact") || t.Contains("alien") => AnomalyType.AlienArtifact,
            var t when t.Contains("stellar") || t.Contains("star") => AnomalyType.StellarPhenomenon,
            var t when t.Contains("dimensional") || t.Contains("tear") => AnomalyType.DimensionalTear,
            _ => AnomalyType.Unknown
        };
    }
}
```

### Step 4: Classification Activity
**ClassifyAnomalyActivity.cs**
```csharp
using Dapr.AI.Conversation;
using Dapr.AI.Conversation.Extensions;
using System.Text.Json;

public class ClassifyAnomalyActivity : WorkflowActivity<SpaceAnomaly, AnomalyClassification>
{
    private readonly DaprConversationClient _conversationClient;
    
    public ClassifyAnomalyActivity(DaprConversationClient conversationClient)
    {
        _conversationClient = conversationClient;
    }
    
    public override async Task<AnomalyClassification> RunAsync(
        WorkflowActivityContext context, 
        SpaceAnomaly input)
    {
        var sensorSummary = $@"
Sensor Data: {input.SensorData}
Coordinates: {input.Coordinates}
Measurements: {string.Join(", ", input.Measurements.Select(m => $"{m.Key}={m.Value}"))}
";

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
                            new MessageContent(@"You are an advanced anomaly classification AI for a deep space 
                            science station. Classify space anomalies into one of these categories:
                            
                            1. TEMPORAL RIFT - Time distortions, chronological anomalies, causality violations
                            2. DARK MATTER CLUSTER - Unusual gravitational fields, invisible mass concentrations
                            3. ALIEN ARTIFACT - Manufactured objects, ancient technology, non-natural structures
                            4. STELLAR PHENOMENON - Star-related events, supernovae, neutron star activity
                            5. DIMENSIONAL TEAR - Reality breaches, multiverse intrusions, spatial ruptures
                            
                            Respond in JSON format:
                            {
                              ""type"": ""<category name>"",
                              ""confidence"": <0.0 to 1.0>,
                              ""reasoning"": ""<brief explanation>""
                            }")
                        ]
                    },
                    new UserMessage
                    {
                        Name = "AnomalyClassifier",
                        Content = [
                            new MessageContent($"Classify this anomaly:\n{sensorSummary}")
                        ]
                    }
                })
            ],
            conversationOptions);
        
        var json = JsonSerializer.Deserialize<JsonElement>(
            response.Outputs.First().Choices.First().Message.Content);
        
        return new AnomalyClassification(
            json.GetProperty("type").GetString(),
            json.GetProperty("confidence").GetDouble(),
            json.GetProperty("reasoning").GetString()
        );
    }
}
```

### Step 5: Specialized Analysis Activities

> **Note**: All activity classes use `DaprConversationClient` with constructor injection. The pattern is:
> ```csharp
> using Dapr.AI.Conversation;
> using Dapr.AI.Conversation.Extensions;
> 
> public class YourActivity : WorkflowActivity<InputType, OutputType>
> {
>     private readonly DaprConversationClient _conversationClient;
>     
>     public YourActivity(DaprConversationClient conversationClient)
>     {
>         _conversationClient = conversationClient;
>     }
> }
> ```

**AnalyzeTemporalRiftActivity.cs**
```csharp
public record TemporalAnalysis(
    string Analysis,
    Dictionary<string, object> QuantumMetrics,
    List<string> SafetyProtocols,
    string TimelineStability
);

public class AnalyzeTemporalRiftActivity : WorkflowActivity<SpaceAnomaly, TemporalAnalysis>
{
    private readonly DaprClient _daprClient;
    
    public override async Task<TemporalAnalysis> RunAsync(
        WorkflowActivityContext context, 
        SpaceAnomaly input)
    {
        var conversationRequest = new
        {
            model = "gpt-4o",
            messages = new[]
            {
                new { 
                    role = "system", 
                    content = @"You are a quantum chronodynamics specialist. Analyze temporal 
                    rifts for:
                    - Timeline divergence probability
                    - Causality violation risk
                    - Temporal radiation levels
                    - Safe approach vectors
                    - Potential for time travel research
                    
                    Provide detailed technical analysis with quantum metrics and safety protocols.
                    Respond in JSON format with: analysis, quantumMetrics (dict), 
                    safetyProtocols (list), timelineStability (LOW/MEDIUM/HIGH/CRITICAL)." 
                },
                new { 
                    role = "user", 
                    content = $"Analyze temporal rift: {input.SensorData}" 
                }
            },
            response_format = new { type = "json_object" }
        };
        
        var response = await _daprClient.InvokeMethodAsync<object, ConversationResponse>(
            HttpMethod.Post,
            "conversation",
            "v1/chat/completions",
            conversationRequest);
        
        var json = JsonSerializer.Deserialize<JsonElement>(
            response.Choices[0].Message.Content);
        
        return new TemporalAnalysis(
            json.GetProperty("analysis").GetString(),
            JsonSerializer.Deserialize<Dictionary<string, object>>(
                json.GetProperty("quantumMetrics").GetRawText()),
            JsonSerializer.Deserialize<List<string>>(
                json.GetProperty("safetyProtocols").GetRawText()),
            json.GetProperty("timelineStability").GetString()
        );
    }
}
```

**AnalyzeDarkMatterActivity.cs**
```csharp
public record DarkMatterAnalysis(
    string Analysis,
    Dictionary<string, object> GravitationalData,
    List<string> HarvestingOpportunities,
    string CollapseProbability
);

public class AnalyzeDarkMatterActivity : WorkflowActivity<SpaceAnomaly, DarkMatterAnalysis>
{
    private readonly DaprClient _daprClient;
    
    public override async Task<DarkMatterAnalysis> RunAsync(
        WorkflowActivityContext context, 
        SpaceAnomaly input)
    {
        var conversationRequest = new
        {
            model = "gpt-4o",
            messages = new[]
            {
                new { 
                    role = "system", 
                    content = @"You are a dark matter physicist. Analyze dark matter clusters for:
                    - Mass concentration and distribution
                    - Gravitational lensing effects
                    - Exotic matter harvesting potential
                    - Black hole formation risk
                    - Energy extraction possibilities
                    
                    Respond in JSON format with: analysis, gravitationalData (dict), 
                    harvestingOpportunities (list), collapseProbability (LOW/MEDIUM/HIGH/CRITICAL)." 
                },
                new { 
                    role = "user", 
                    content = $"Analyze dark matter cluster: {input.SensorData}" 
                }
            },
            response_format = new { type = "json_object" }
        };
        
        var response = await _daprClient.InvokeMethodAsync<object, ConversationResponse>(
            HttpMethod.Post,
            "conversation",
            "v1/chat/completions",
            conversationRequest);
        
        var json = JsonSerializer.Deserialize<JsonElement>(
            response.Choices[0].Message.Content);
        
        return new DarkMatterAnalysis(
            json.GetProperty("analysis").GetString(),
            JsonSerializer.Deserialize<Dictionary<string, object>>(
                json.GetProperty("gravitationalData").GetRawText()),
            JsonSerializer.Deserialize<List<string>>(
                json.GetProperty("harvestingOpportunities").GetRawText()),
            json.GetProperty("collapseProbability").GetString()
        );
    }
}
```

**AnalyzeAlienArtifactActivity.cs**
```csharp
public record ArtifactAnalysis(
    string Analysis,
    Dictionary<string, object> XenoarchaeologyData,
    List<string> ExtractionProcedures,
    string HostilityIndicators
);

public class AnalyzeAlienArtifactActivity : WorkflowActivity<SpaceAnomaly, ArtifactAnalysis>
{
    private readonly DaprClient _daprClient;
    
    public override async Task<ArtifactAnalysis> RunAsync(
        WorkflowActivityContext context, 
        SpaceAnomaly input)
    {
        var conversationRequest = new
        {
            model = "gpt-4o",
            messages = new[]
            {
                new { 
                    role = "system", 
                    content = @"You are a xenoarchaeologist specializing in alien artifacts. Analyze for:
                    - Estimated age and civilization of origin
                    - Technology level and purpose
                    - Active vs dormant status
                    - Defensive mechanisms or traps
                    - Cultural and scientific value
                    - Safe extraction procedures
                    
                    Respond in JSON format with: analysis, xenoarchaeologyData (dict), 
                    extractionProcedures (list), hostilityIndicators (SAFE/CAUTION/DANGEROUS/LETHAL)." 
                },
                new { 
                    role = "user", 
                    content = $"Analyze alien artifact: {input.SensorData}" 
                }
            },
            response_format = new { type = "json_object" }
        };
        
        var response = await _daprClient.InvokeMethodAsync<object, ConversationResponse>(
            HttpMethod.Post,
            "conversation",
            "v1/chat/completions",
            conversationRequest);
        
        var json = JsonSerializer.Deserialize<JsonElement>(
            response.Choices[0].Message.Content);
        
        return new ArtifactAnalysis(
            json.GetProperty("analysis").GetString(),
            JsonSerializer.Deserialize<Dictionary<string, object>>(
                json.GetProperty("xenoarchaeologyData").GetRawText()),
            JsonSerializer.Deserialize<List<string>>(
                json.GetProperty("extractionProcedures").GetRawText()),
            json.GetProperty("hostilityIndicators").GetString()
        );
    }
}
```

### Step 6: API Endpoints
**Program.cs**
```csharp
using Dapr.Client;
using Dapr.Workflow;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDaprClient();
builder.Services.AddDaprConversationClient();
builder.Services.AddDaprWorkflow(options =>
{
    options.RegisterWorkflow<AnomalyRoutingWorkflow>();
    options.RegisterActivity<ClassifyAnomalyActivity>();
    options.RegisterActivity<AnalyzeTemporalRiftActivity>();
    options.RegisterActivity<AnalyzeDarkMatterActivity>();
    options.RegisterActivity<AnalyzeAlienArtifactActivity>();
    options.RegisterActivity<AnalyzeStellarPhenomenonActivity>();
    options.RegisterActivity<AnalyzeDimensionalTearActivity>();
});

var app = builder.Build();

// Analyze a new anomaly
app.MapPost("/anomaly/analyze", async (
    SpaceAnomaly anomaly,
    DaprWorkflowClient workflowClient,
    DaprClient daprClient) =>
{
    var instanceId = $"anomaly-{anomaly.AnomalyId}";
    
    // Store original anomaly data
    await daprClient.SaveStateAsync(
        "statestore", 
        $"anomaly-{anomaly.AnomalyId}", 
        anomaly);
    
    // Start routing workflow
    await workflowClient.ScheduleNewWorkflowAsync(
        nameof(AnomalyRoutingWorkflow),
        instanceId,
        anomaly);
    
    return Results.Accepted($"/anomaly/status/{instanceId}", new { instanceId });
});

// Get analysis status and results
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

// Get a specific analysis result by anomaly ID
app.MapGet("/anomalies/{anomalyId}", async (
    string anomalyId,
    DaprClient daprClient) =>
{
    var result = await daprClient.GetStateAsync<AnalysisResult>(
        "statestore",
        anomalyId);
    
    if (result == null)
        return Results.NotFound();
    
    return Results.Ok(result);
});

// Get statistics on anomaly classifications
app.MapGet("/anomalies/stats", async (DaprClient daprClient) =>
{
    // This would query all anomalies and aggregate statistics
    // Simplified for demo purposes
    return Results.Ok(new
    {
        total = 42,
        byType = new Dictionary<string, int>
        {
            ["TemporalRift"] = 8,
            ["DarkMatterCluster"] = 15,
            ["AlienArtifact"] = 5,
            ["StellarPhenomenon"] = 12,
            ["DimensionalTear"] = 2
        }
    });
});

app.Run();
```

### Step 7: Dapr MultiApp Run
**dapr.yaml**
```yaml
version: 1
common:
  resourcesPath: Resources
  appLogDestination: fileAndConsole
  daprdLogDestination: fileAndConsole
apps:
  - appID: anomaly-routing-app
    appDirPath: /
    appPort: 5500
    daprHTTPPort: 3550
    command: ["dotnet", "run"]
```

### Step 8: Local HTTP Tests
**local.http**
```http
### Analyze a temporal rift
POST http://localhost:5000/anomaly/analyze
Content-Type: application/json

{
  "anomalyId": "ANM-001",
  "sensorData": "Chronal flux readings off the scale. Time dilation factor: 847.2. Causality loops detected in 3-dimensional space. Tachyon particles streaming from epicenter.",
  "coordinates": "Sector 7-G, Quadrant Epsilon",
  "detectedAt": "2425-03-15T10:22:00Z",
  "measurements": {
    "chronalFlux": 847.2,
    "timeDialation": 3.4,
    "tachyonDensity": 0.00042,
    "causalityIndex": -2.1
  }
}

### Analyze dark matter cluster
POST http://localhost:5000/anomaly/analyze
Content-Type: application/json

{
  "anomalyId": "ANM-002",
  "sensorData": "Massive gravitational anomaly. No visible matter detected. Gravitational lensing severe. Estimated mass: 10^6 solar masses in 5 cubic light-years.",
  "coordinates": "Outer rim, Nebula X-42",
  "detectedAt": "2425-03-15T11:45:00Z",
  "measurements": {
    "gravitationalPull": 9.8e12,
    "mass": 1.989e36,
    "volume": 1.18e50,
    "lensingFactor": 4.7
  }
}

### Analyze alien artifact
POST http://localhost:5000/anomaly/analyze
Content-Type: application/json

{
  "anomalyId": "ANM-003",
  "sensorData": "Geometric structure detected. Composed of unknown alloy. Emitting low-frequency EM pulses in prime number sequences. Surface temperature: -270C despite no heat source nearby.",
  "coordinates": "Derelict orbit around pulsar PSR J0737",
  "detectedAt": "2425-03-15T13:10:00Z",
  "measurements": {
    "size": 47.5,
    "temperature": -270,
    "emFrequency": 1.42e9,
    "materialDensity": 22.6
  }
}

### Get analysis status
GET http://localhost:5000/anomaly/status/anomaly-ANM-001

### Get all temporal rifts
GET http://localhost:5000/anomalies/by-type/TemporalRift

### Get anomaly statistics
GET http://localhost:5000/anomalies/stats
```

## Benefits of Routing Pattern

### 1. **Specialized Optimization**
Each route can use:
- **Different models** - Use smaller/cheaper models for simple stellar phenomena, powerful models for complex dimensional tears
- **Specialized prompts** - Xenoarchaeology expertise for artifacts, quantum physics for temporal rifts
- **Custom tools** - Each route can have type-specific tools and data sources
- **Different parameters** - Adjust temperature, max tokens, etc. per route

### 2. **Better Performance per Category**
A generic "analyze any anomaly" prompt would perform poorly across all types. Routing enables:
- Deep expertise in each domain
- Context-specific reasoning
- Appropriate technical vocabulary
- Domain-relevant safety protocols

### 3. **Clean Separation of Concerns**
Each specialized activity is:
- Independently developable and testable
- Maintainable by domain experts
- Evolvable without affecting other routes
- Deployable/scalable separately if needed

### 4. **Efficient Resource Usage**
- Simple cases use cheap/fast routes
- Complex cases get powerful/expensive analysis
- No wasted compute on unnecessary processing
- Can scale routes independently based on volume

### 5. **Easy to Extend**
Adding a new anomaly type requires:
1. Add classification logic
2. Create specialized activity
3. Add route case in workflow
No changes to existing routes needed.

### 6. **Clear Metrics per Route**
Can track separately:
- Accuracy by anomaly type
- Latency per route
- Cost per classification
- Error rates by specialist

## Drawbacks of Routing Pattern

### 1. **Classification Accuracy Critical**
The entire system depends on correct initial classification:
- Misclassified artifact as stellar phenomenon → wrong analysis pipeline
- No easy recovery if classification is wrong
- Errors compound - bad route = bad result
- May need human validation for edge cases

### 2. **Extra Classification Latency**
Every anomaly pays classification cost:
- Additional LLM call before specialized analysis
- Even obvious cases (clear temporal signatures) wait for classification
- Could optimize with rule-based pre-classification for clear cases

### 3. **Maintenance Overhead**
More moving parts to maintain:
- N specialized activities vs 1 generic one
- Each route needs separate prompt engineering
- Testing requires coverage across all routes
- Updates may need coordination across activities

### 4. **Handling Edge Cases**
Anomalies that don't fit categories cleanly:
- Hybrid anomalies (temporal rift + dimensional tear)
- Partially matching signatures
- Unknown/novel anomaly types
- Current system has weak fallback for "Unknown"

### 5. **Potential for Code Duplication**
Different specialized activities may share logic:
- Common safety check patterns
- Similar metric calculations
- Overlapping recommendation logic
- Requires discipline to extract shared code

### 6. **Uneven Route Complexity**
Some routes may be:
- Much more complex than others (temporal rifts vs stellar phenomena)
- Rarely used (dimensional tears are rarer)
- Disproportionately expensive
- Hard to validate due to low volume

## When to Use This Pattern

**Use routing when:**
- Clear, distinct categories exist
- Different categories need fundamentally different handling
- Classification can be done accurately
- Specialization improves results significantly
- Categories have different resource requirements

**Avoid routing when:**
- Categories are fuzzy or overlapping
- Generic approach works well enough
- Classification is unreliable
- Overhead of multiple routes outweighs benefits
- Few distinct categories (2-3) - consider simpler branching

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
Use the VSCode REST Client extension with `local.http` to send various anomaly types.

## Conclusion
The routing workflow pattern excels when you have well-defined categories that benefit from specialized handling. The initial classification step routes each anomaly to the most appropriate analysis pipeline, ensuring expert-level processing while maintaining efficiency. Dapr Workflow provides the reliability and observability needed for production scientific analysis systems.
