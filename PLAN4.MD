# Plan 4: Workflow Orchestrator-Worker - Space Colony Construction Planner

## Overview
This demo implements an **orchestrator-worker workflow** using Dapr Workflow where Commander William Riker plans and coordinates complex away team missions for the USS Enterprise-D. Riker, as the orchestrator, dynamically determines which crew specialists are needed (Data, Geordi, Worf, Dr. Crusher), delegates detailed preparation to each specialist, and synthesizes the results into a comprehensive mission plan for Captain Picard's approval.

## Sci-Fi Use Case: Commander Riker's Away Team Mission Planning System
You're building a backend system for First Officer William Riker to plan complex away team missions to newly discovered planets. Riker, as the orchestrator, must analyze planetary data from sensors, then dynamically assemble the right away team specialists and coordinate their mission objectives:
1. **Analyze** planetary conditions using Data's sensor analysis (gravity, atmosphere, radiation, life signs)
2. **Determine** what specialists are needed (Geordi for technology, Dr. Crusher for biology, Worf for security, etc.)
3. **Delegate** detailed preparation to each specialist (equipment, protocols, safety measures)
4. **Synthesize** individual plans into a coordinated away team mission brief
5. **Optimize** resource allocation and mission sequencing for Picard's approval

The key difference from parallelization: Riker doesn't know ahead of time which crew members are needed or how many specialists to assign—he decides dynamically based on the planet's unique characteristics and mission complexity.

## Architecture

### Services
1. **ColonyPlannerAPI** - ASP.NET Core Web API (Entry point)
2. **ColonyOrchestratorWorkflow** - Main orchestrator workflow
3. **WorkerActivities** - Specialized planning activities for different structure types
4. **AnalysisActivities** - Planetary analysis and synthesis activities

### Components Used
- **Dapr Workflow** - Orchestrates dynamic task delegation
- **Dapr State Management** - Stores colony plans and construction progress
- **Dapr Conversation API** - Powers orchestrator decisions and worker planning

## Detailed Implementation Plan

### Step 1: Project Setup
```
/SpaceColonyPlanner
  - SpaceColonyPlanner.csproj
  - Program.cs
  - local.http
  - appsettings.json
  - dapr.yaml
  /Workflows
    - ColonyOrchestratorWorkflow.cs
  /Activities
    /Analysis
      - AnalyzePlanetActivity.cs
      - DetermineStructuresActivity.cs
      - SynthesizePlanActivity.cs
      - OptimizeTimelineActivity.cs
    /Workers
      - PlanHabitatDomeActivity.cs
      - PlanPowerPlantActivity.cs
      - PlanAgricultureActivity.cs
      - PlanMiningFacilityActivity.cs
      - PlanResearchLabActivity.cs
      - PlanDefenseSystemActivity.cs
  /Models
    - Planet.cs
    - ColonyRequirements.cs
    - StructureRequest.cs
    - StructurePlan.cs
    - ColonyMasterPlan.cs
  /Resources
    - statestore.yaml
    - conversation.yaml
  /Properties
    - launchSettings.json
```

### Step 2: Core Models
**Planet.cs**
```csharp
public record Planet(
    string PlanetId,
    string Name,
    PlanetaryConditions Conditions,
    AvailableResources Resources
);

public record PlanetaryConditions(
    double Gravity, // Earth = 1.0
    string AtmosphereType, // "None", "Toxic", "Breathable", "Thin"
    double Temperature, // Celsius
    double RadiationLevel, // Sv/year
    bool HasWater,
    double DayLength // Earth days
);

public record AvailableResources(
    bool Metals,
    bool RareEarths,
    bool Water,
    bool Organics,
    bool Uranium,
    string SoilQuality // "None", "Poor", "Fair", "Good"
);

public record ColonyRequirements(
    int InitialPopulation,
    int TargetPopulation,
    string Purpose, // "Mining", "Research", "Agriculture", "Military", "Mixed"
    int YearsToComplete
);

public record StructureRequest(
    string StructureType,
    string Priority, // "Critical", "High", "Medium", "Low"
    int Quantity,
    string Reasoning
);

public record StructurePlan(
    string StructureType,
    int Quantity,
    List<string> Materials,
    int ConstructionDays,
    int WorkerHours,
    List<string> Prerequisites,
    string DetailedSpecification
);

public record ColonyMasterPlan(
    string PlanetId,
    List<StructurePlan> Structures,
    int TotalConstructionDays,
    Dictionary<string, int> MaterialsRequired,
    List<ConstructionPhase> Timeline,
    string SuccessFactors,
    string RiskAssessment
);

public record ConstructionPhase(
    int PhaseNumber,
    string Name,
    List<string> Structures,
    int DurationDays
);
```

### Step 3: Orchestrator Workflow
**ColonyOrchestratorWorkflow.cs**
```csharp
public class ColonyOrchestratorWorkflow : Workflow<ColonyRequest, ColonyMasterPlan>
{
    public override async Task<ColonyMasterPlan> RunAsync(
        WorkflowContext context, 
        ColonyRequest input)
    {
        // Step 1: Analyze planet to understand constraints
        var planetAnalysis = await context.CallActivityAsync<PlanetAnalysis>(
            nameof(AnalyzePlanetActivity),
            input.Planet);
        
        // Step 2: Orchestrator determines what structures are needed
        // This is DYNAMIC - different planets need different structures
        var structureRequests = await context.CallActivityAsync<List<StructureRequest>>(
            nameof(DetermineStructuresActivity),
            new { input.Planet, input.Requirements, planetAnalysis });
        
        // Step 3: Dynamically spawn worker tasks for each structure type
        // The orchestrator doesn't know ahead of time how many workers needed!
        var workerTasks = new List<Task<StructurePlan>>();
        
        foreach (var request in structureRequests)
        {
            // Route to appropriate specialist worker based on structure type
            var workerActivity = request.StructureType switch
            {
                "HabitatDome" => nameof(PlanHabitatDomeActivity),
                "PowerPlant" => nameof(PlanPowerPlantActivity),
                "Agriculture" => nameof(PlanAgricultureActivity),
                "MiningFacility" => nameof(PlanMiningFacilityActivity),
                "ResearchLab" => nameof(PlanResearchLabActivity),
                "DefenseSystem" => nameof(PlanDefenseSystemActivity),
                _ => throw new InvalidOperationException($"Unknown structure type: {request.StructureType}")
            };
            
            var workerInput = new WorkerInput(
                request,
                input.Planet,
                planetAnalysis
            );
            
            workerTasks.Add(
                context.CallActivityAsync<StructurePlan>(workerActivity, workerInput)
            );
        }
        
        // Wait for all workers to complete their specialized planning
        var structurePlans = await Task.WhenAll(workerTasks);
        
        // Step 4: Synthesize individual plans into master plan
        var masterPlan = await context.CallActivityAsync<ColonyMasterPlan>(
            nameof(SynthesizePlanActivity),
            new { 
                PlanetId = input.Planet.PlanetId,
                StructurePlans = structurePlans.ToList(),
                Requirements = input.Requirements
            });
        
        // Step 5: Optimize construction timeline
        var optimizedPlan = await context.CallActivityAsync<ColonyMasterPlan>(
            nameof(OptimizeTimelineActivity),
            masterPlan);
        
        return optimizedPlan;
    }
}

public record ColonyRequest(Planet Planet, ColonyRequirements Requirements);
public record WorkerInput(StructureRequest Request, Planet Planet, PlanetAnalysis Analysis);
public record PlanetAnalysis(
    List<string> Challenges,
    List<string> Opportunities,
    string RecommendedApproach
);
```

### Step 4: Orchestrator Activities

> **Note**: All orchestrator and worker activities use `DaprConversationClient` with constructor injection. Each activity creates `ConversationOptions` with the conversation component name, temperature, and model, then calls `ConverseAsync()` with `SystemMessage` and `UserMessage`. See PLAN1 for complete implementation examples.

**AnalyzePlanetActivity.cs**
```csharp
public class AnalyzePlanetActivity : WorkflowActivity<Planet, PlanetAnalysis>
{
    private readonly DaprClient _daprClient;
    
    public override async Task<PlanetAnalysis> RunAsync(
        WorkflowActivityContext context, 
        Planet input)
    {
        var conversationRequest = new
        {
            model = "gpt-4o",
            messages = new[]
            {
                new { 
                    role = "system", 
                    content = @"You are a planetary colonization expert. Analyze planets for 
                    colonization challenges and opportunities. Consider:
                    - Environmental hazards (radiation, temperature, atmosphere)
                    - Available resources
                    - Engineering requirements
                    - Long-term sustainability
                    
                    Return JSON with: challenges (list), opportunities (list), 
                    recommendedApproach (string)." 
                },
                new { 
                    role = "user", 
                    content = $@"Analyze planet for colonization:
Planet: {input.Name}
Gravity: {input.Conditions.Gravity}g
Atmosphere: {input.Conditions.AtmosphereType}
Temperature: {input.Conditions.Temperature}°C
Radiation: {input.Conditions.RadiationLevel} Sv/year
Has Water: {input.Conditions.HasWater}
Day Length: {input.Conditions.DayLength} Earth days

Resources:
- Metals: {input.Resources.Metals}
- Rare Earths: {input.Resources.RareEarths}
- Water: {input.Resources.Water}
- Organics: {input.Resources.Organics}
- Uranium: {input.Resources.Uranium}
- Soil: {input.Resources.SoilQuality}" 
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
        
        return new PlanetAnalysis(
            JsonSerializer.Deserialize<List<string>>(
                json.GetProperty("challenges").GetRawText()),
            JsonSerializer.Deserialize<List<string>>(
                json.GetProperty("opportunities").GetRawText()),
            json.GetProperty("recommendedApproach").GetString()
        );
    }
}
```

**DetermineStructuresActivity.cs**
```csharp
public class DetermineStructuresActivity : WorkflowActivity<object, List<StructureRequest>>
{
    private readonly DaprClient _daprClient;
    
    public override async Task<List<StructureRequest>> RunAsync(
        WorkflowActivityContext context, 
        object input)
    {
        var data = (dynamic)input;
        Planet planet = data.Planet;
        ColonyRequirements requirements = data.Requirements;
        PlanetAnalysis analysis = data.planetAnalysis;
        
        var conversationRequest = new
        {
            model = "gpt-4o",
            messages = new[]
            {
                new { 
                    role = "system", 
                    content = @"You are a colony planning AI. Determine what structures are 
                    needed for a successful colony. Consider:
                    - Environmental protection needs
                    - Resource availability
                    - Population requirements
                    - Colony purpose
                    
                    Available structure types:
                    - HabitatDome (housing, life support)
                    - PowerPlant (energy generation)
                    - Agriculture (food production)
                    - MiningFacility (resource extraction)
                    - ResearchLab (scientific research)
                    - DefenseSystem (protection from threats)
                    
                    Return JSON array of structures:
                    [{
                      ""structureType"": ""HabitatDome"",
                      ""priority"": ""Critical|High|Medium|Low"",
                      ""quantity"": 3,
                      ""reasoning"": ""explanation""
                    }]" 
                },
                new { 
                    role = "user", 
                    content = $@"Determine structures needed:

Planet Challenges: {string.Join(", ", analysis.Challenges)}
Planet Opportunities: {string.Join(", ", analysis.Opportunities)}

Colony Requirements:
- Population: {requirements.InitialPopulation} → {requirements.TargetPopulation}
- Purpose: {requirements.Purpose}
- Timeline: {requirements.YearsToComplete} years

What structures are needed?" 
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
        
        var structures = json.GetProperty("structures");
        var result = new List<StructureRequest>();
        
        foreach (var item in structures.EnumerateArray())
        {
            result.Add(new StructureRequest(
                item.GetProperty("structureType").GetString(),
                item.GetProperty("priority").GetString(),
                item.GetProperty("quantity").GetInt32(),
                item.GetProperty("reasoning").GetString()
            ));
        }
        
        return result;
    }
}
```

### Step 5: Worker Activities (Specialized Planners)

**PlanHabitatDomeActivity.cs**
```csharp
public class PlanHabitatDomeActivity : WorkflowActivity<WorkerInput, StructurePlan>
{
    private readonly DaprClient _daprClient;
    
    public override async Task<StructurePlan> RunAsync(
        WorkflowActivityContext context, 
        WorkerInput input)
    {
        var conversationRequest = new
        {
            model = "gpt-4o",
            messages = new[]
            {
                new { 
                    role = "system", 
                    content = @"You are a habitat dome construction specialist. Design detailed 
                    plans for pressurized living environments. Consider:
                    - Atmospheric containment for planet conditions
                    - Radiation shielding requirements
                    - Life support systems
                    - Living space per person
                    - Emergency backup systems
                    
                    Return JSON with: structureType, quantity, materials (list), 
                    constructionDays, workerHours, prerequisites (list), 
                    detailedSpecification (string)." 
                },
                new { 
                    role = "user", 
                    content = $@"Plan habitat domes:
Quantity Needed: {input.Request.Quantity}
Priority: {input.Request.Priority}

Planet Conditions:
- Atmosphere: {input.Planet.Conditions.AtmosphereType}
- Radiation: {input.Planet.Conditions.RadiationLevel} Sv/year
- Temperature: {input.Planet.Conditions.Temperature}°C

Challenges: {string.Join(", ", input.Analysis.Challenges)}

Provide detailed construction plan." 
                }
            },
            response_format = new { type = "json_object" }
        };
        
        var response = await _daprClient.InvokeMethodAsync<object, ConversationResponse>(
            HttpMethod.Post,
            "conversation",
            "v1/chat/completions",
            conversationRequest);
        
        return ParseStructurePlan(response.Choices[0].Message.Content);
    }
    
    private StructurePlan ParseStructurePlan(string jsonContent)
    {
        var json = JsonSerializer.Deserialize<JsonElement>(jsonContent);
        return new StructurePlan(
            json.GetProperty("structureType").GetString(),
            json.GetProperty("quantity").GetInt32(),
            JsonSerializer.Deserialize<List<string>>(
                json.GetProperty("materials").GetRawText()),
            json.GetProperty("constructionDays").GetInt32(),
            json.GetProperty("workerHours").GetInt32(),
            JsonSerializer.Deserialize<List<string>>(
                json.GetProperty("prerequisites").GetRawText()),
            json.GetProperty("detailedSpecification").GetString()
        );
    }
}
```

**PlanPowerPlantActivity.cs**
```csharp
public class PlanPowerPlantActivity : WorkflowActivity<WorkerInput, StructurePlan>
{
    private readonly DaprClient _daprClient;
    
    public override async Task<StructurePlan> RunAsync(
        WorkflowActivityContext context, 
        WorkerInput input)
    {
        var conversationRequest = new
        {
            model = "gpt-4o",
            messages = new[]
            {
                new { 
                    role = "system", 
                    content = @"You are a power generation specialist. Design power plants 
                    appropriate for planetary conditions. Options:
                    - Solar (if adequate sunlight)
                    - Nuclear (if uranium available)
                    - Geothermal (if volcanic activity)
                    - Fusion (most reliable but complex)
                    
                    Return detailed construction plan in JSON format." 
                },
                new { 
                    role = "user", 
                    content = $@"Plan power generation:
Quantity: {input.Request.Quantity}

Planet:
- Day Length: {input.Planet.Conditions.DayLength} Earth days
- Uranium Available: {input.Planet.Resources.Uranium}
- Temperature: {input.Planet.Conditions.Temperature}°C

Design appropriate power plant(s)." 
                }
            },
            response_format = new { type = "json_object" }
        };
        
        var response = await _daprClient.InvokeMethodAsync<object, ConversationResponse>(
            HttpMethod.Post,
            "conversation",
            "v1/chat/completions",
            conversationRequest);
        
        return ParseStructurePlan(response.Choices[0].Message.Content);
    }
}
```

**PlanAgricultureActivity.cs**
```csharp
public class PlanAgricultureActivity : WorkflowActivity<WorkerInput, StructurePlan>
{
    private readonly DaprClient _daprClient;
    
    public override async Task<StructurePlan> RunAsync(
        WorkflowActivityContext context, 
        WorkerInput input)
    {
        var conversationRequest = new
        {
            model = "gpt-4o",
            messages = new[]
            {
                new { 
                    role = "system", 
                    content = @"You are an agricultural engineer specializing in off-world 
                    farming. Design food production facilities considering:
                    - Hydroponic vs soil-based farming
                    - Artificial lighting needs
                    - Water recycling systems
                    - Calorie production per person
                    
                    Return detailed plan in JSON." 
                },
                new { 
                    role = "user", 
                    content = $@"Plan agricultural facilities:
Population: will grow to target size
Quantity: {input.Request.Quantity}

Planet:
- Water Available: {input.Planet.Resources.Water}
- Soil Quality: {input.Planet.Resources.SoilQuality}
- Organics: {input.Planet.Resources.Organics}

Design food production system." 
                }
            },
            response_format = new { type = "json_object" }
        };
        
        var response = await _daprClient.InvokeMethodAsync<object, ConversationResponse>(
            HttpMethod.Post,
            "conversation",
            "v1/chat/completions",
            conversationRequest);
        
        return ParseStructurePlan(response.Choices[0].Message.Content);
    }
}
```

### Step 6: Synthesis Activity
**SynthesizePlanActivity.cs**
```csharp
public class SynthesizePlanActivity : WorkflowActivity<object, ColonyMasterPlan>
{
    private readonly DaprClient _daprClient;
    
    public override async Task<ColonyMasterPlan> RunAsync(
        WorkflowActivityContext context, 
        object input)
    {
        var data = (dynamic)input;
        string planetId = data.PlanetId;
        List<StructurePlan> plans = data.StructurePlans;
        
        // Aggregate materials
        var allMaterials = new Dictionary<string, int>();
        foreach (var plan in plans)
        {
            foreach (var material in plan.Materials)
            {
                if (allMaterials.ContainsKey(material))
                    allMaterials[material] += plan.Quantity;
                else
                    allMaterials[material] = plan.Quantity;
            }
        }
        
        // Create construction timeline by analyzing prerequisites
        var conversationRequest = new
        {
            model = "gpt-4o",
            messages = new[]
            {
                new { 
                    role = "system", 
                    content = @"You are a construction project manager. Organize structures 
                    into construction phases based on prerequisites and efficiency. 
                    
                    Return JSON with:
                    - timeline: array of phases [{phaseNumber, name, structures[], durationDays}]
                    - successFactors: string
                    - riskAssessment: string" 
                },
                new { 
                    role = "user", 
                    content = $@"Organize construction timeline:
Structures: {JsonSerializer.Serialize(plans)}

Create phased construction plan." 
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
        
        var timeline = new List<ConstructionPhase>();
        foreach (var phase in json.GetProperty("timeline").EnumerateArray())
        {
            timeline.Add(new ConstructionPhase(
                phase.GetProperty("phaseNumber").GetInt32(),
                phase.GetProperty("name").GetString(),
                JsonSerializer.Deserialize<List<string>>(
                    phase.GetProperty("structures").GetRawText()),
                phase.GetProperty("durationDays").GetInt32()
            ));
        }
        
        return new ColonyMasterPlan(
            planetId,
            plans,
            timeline.Sum(p => p.DurationDays),
            allMaterials,
            timeline,
            json.GetProperty("successFactors").GetString(),
            json.GetProperty("riskAssessment").GetString()
        );
    }
}
```

### Step 7: API Endpoints
**Program.cs**
```csharp
using Dapr.Client;
using Dapr.Workflow;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDaprClient();
builder.Services.AddDaprConversationClient();
builder.Services.AddDaprWorkflow(options =>
{
    options.RegisterWorkflow<ColonyOrchestratorWorkflow>();
    
    // Analysis activities
    options.RegisterActivity<AnalyzePlanetActivity>();
    options.RegisterActivity<DetermineStructuresActivity>();
    options.RegisterActivity<SynthesizePlanActivity>();
    options.RegisterActivity<OptimizeTimelineActivity>();
    
    // Worker activities
    options.RegisterActivity<PlanHabitatDomeActivity>();
    options.RegisterActivity<PlanPowerPlantActivity>();
    options.RegisterActivity<PlanAgricultureActivity>();
    options.RegisterActivity<PlanMiningFacilityActivity>();
    options.RegisterActivity<PlanResearchLabActivity>();
    options.RegisterActivity<PlanDefenseSystemActivity>();
});

var app = builder.Build();

// Plan a new colony
app.MapPost("/colony/plan", async (
    ColonyRequest request,
    DaprWorkflowClient workflowClient,
    DaprClient daprClient) =>
{
    var instanceId = $"colony-{request.Planet.PlanetId}-{DateTime.UtcNow.Ticks}";
    
    // Save planet data
    await daprClient.SaveStateAsync(
        "statestore",
        $"planet-{request.Planet.PlanetId}",
        request.Planet);
    
    // Start orchestrator workflow
    await workflowClient.ScheduleNewWorkflowAsync(
        nameof(ColonyOrchestratorWorkflow),
        instanceId,
        request);
    
    return Results.Accepted($"/colony/plan/{instanceId}", new { instanceId });
});

// Get colony plan
app.MapGet("/colony/plan/{instanceId}", async (
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
        plan = state.ReadOutputAs<ColonyMasterPlan>(),
        createdAt = state.CreatedAt
    });
});

app.Run();
```

### Step 8: Dapr MultiApp Run
**dapr.yaml**
```yaml
version: 1
common:
  resourcesPath: Resources
  appLogDestination: fileAndConsole
  daprdLogDestination: fileAndConsole
apps:
  - appID: away-team-planner-app
    appDirPath: /
    appPort: 5500
    daprHTTPPort: 3550
    command: ["dotnet", "run"]
```

### Step 9: Local HTTP Tests
**local.http**
```http
### Plan a harsh desert planet colony
POST http://localhost:5500/colony/plan
Content-Type: application/json

{
  "planet": {
    "planetId": "AR-558",
    "name": "Arrakis Minor",
    "conditions": {
      "gravity": 0.9,
      "atmosphereType": "Thin",
      "temperature": 45,
      "radiationLevel": 0.5,
      "hasWater": false,
      "dayLength": 1.2
    },
    "resources": {
      "metals": true,
      "rareEarths": true,
      "water": false,
      "organics": false,
      "uranium": false,
      "soilQuality": "Poor"
    }
  },
  "requirements": {
    "initialPopulation": 100,
    "targetPopulation": 5500,
    "purpose": "Mining",
    "yearsToComplete": 5
  }
}

### Plan an ice world research colony
POST http://localhost:5500/colony/plan
Content-Type: application/json

{
  "planet": {
    "planetId": "HOTH-7",
    "name": "Glacius Prime",
    "conditions": {
      "gravity": 1.1,
      "atmosphereType": "Breathable",
      "temperature": -40,
      "radiationLevel": 0.1,
      "hasWater": true,
      "dayLength": 0.8
    },
    "resources": {
      "metals": false,
      "rareEarths": false,
      "water": true,
      "organics": true,
      "uranium": false,
      "soilQuality": "None"
    }
  },
  "requirements": {
    "initialPopulation": 50,
    "targetPopulation": 500,
    "purpose": "Research",
    "yearsToComplete": 3
  }
}

### Get colony plan
GET http://localhost:5500/colony/plan/colony-AR-558-638742234567890000
```

## Benefits of Orchestrator-Worker Pattern

### 1. **Dynamic Task Decomposition**
The orchestrator decides at runtime:
- How many structures needed (varies by planet)
- Which specialist workers to invoke
- No hardcoded assumptions about task count
- Adapts to unique planetary conditions

### 2. **Intelligent Coordination**
Orchestrator provides context and synthesizes results:
- Workers get relevant planet analysis
- Final synthesis considers all worker outputs together
- Can optimize across worker results
- Maintains coherent overall plan

### 3. **Specialist Expertise**
Each worker is deeply specialized:
- Habitat dome expert knows structural engineering
- Power plant expert knows energy systems
- Agriculture expert knows farming techniques
- Better results than generic "plan everything" prompt

### 4. **Scalability to Complexity**
Handles arbitrarily complex scenarios:
- Simple planet → few structures → few workers
- Complex planet → many structures → many workers
- Automatically scales worker count
- No manual workflow redesign needed

### 5. **Maintainability**
- Add new structure type = add one worker activity
- Update worker logic without touching orchestrator
- Clear separation of concerns
- Each component independently testable

### 6. **Real-World Applicability**
Perfect for scenarios where:
- Requirements vary widely per request
- Task breakdown isn't known ahead of time
- Need both high-level strategy and detailed execution
- Results must be synthesized into coherent plan

## Drawbacks of Orchestrator-Worker Pattern

### 1. **Orchestrator Complexity**
The orchestrator must be sophisticated:
- Intelligent task decomposition
- Dynamic worker selection
- Result synthesis logic
- More complex than fixed workflows

### 2. **Multiple LLM Calls**
Sequential overhead:
1. Analyze planet
2. Determine structures
3. Spawn workers (parallel)
4. Synthesize
5. Optimize

Minimum 4-5 LLM calls even for simple cases.

### 3. **Cost Variability**
Unpredictable cost per request:
- Simple planet: few workers, low cost
- Complex planet: many workers, high cost
- Hard to estimate costs upfront
- Budgeting is challenging

### 4. **Context Management**
Workers need sufficient context:
- Orchestrator must provide relevant planet data
- Workers must understand constraints
- Risk of context loss across calls
- Careful prompt engineering required

### 5. **Debugging Complexity**
Hard to trace issues:
- Which worker produced bad plan?
- Was orchestrator's task decomposition wrong?
- Did synthesis fail to integrate properly?
- Multiple failure points

### 6. **Synthesis Challenges**
Combining worker outputs is non-trivial:
- Workers may produce conflicting requirements
- Prerequisites may form circular dependencies
- Resource allocations may exceed availability
- Requires sophisticated aggregation logic

## When to Use This Pattern

**Use orchestrator-worker when:**
- Task complexity varies per input
- Can't predict subtask count ahead of time
- Need both strategic planning and tactical execution
- Benefit from specialized workers
- Results must be synthesized coherently

**Avoid orchestrator-worker when:**
- Task structure is fixed (use parallelization)
- Sequential processing works (use chaining)
- Simple routing suffices (use routing pattern)
- Cost predictability is critical

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
Use VSCode REST Client with `local.http` to plan colonies on different planets.

## Conclusion
The orchestrator-worker pattern excels for complex, variable tasks like colony planning where the orchestrator intelligently decomposes work and coordinates specialized workers. Each planet's unique conditions result in a dynamically determined set of construction tasks, making this pattern ideal for scenarios where you can't hardcode the workflow structure. Dapr Workflow provides the reliability needed for these complex multi-step processes.
