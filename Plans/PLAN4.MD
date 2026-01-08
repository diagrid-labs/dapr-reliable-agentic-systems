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
    - Enums.cs
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
**Enums.cs**
```csharp
public enum AtmosphereType
{
    None,
    Toxic,
    Breathable,
    Thin
}

public enum SoilQuality
{
    None,
    Poor,
    Fair,
    Good
}

public enum Purpose
{
    Mining,
    Research,
    Agriculture,
    Military,
    Mixed
}

public enum Priority
{
    Critical,
    High,
    Medium,
    Low
}
```

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
    AtmosphereType AtmosphereType,
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
    SoilQuality SoilQuality
);

public record ColonyRequirements(
    int InitialPopulation,
    int TargetPopulation,
    Purpose Purpose,
    int YearsToComplete
);

public record StructureRequest(
    string StructureType,
    Priority Priority,
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

public record DetermineStructuresInput(
    Planet Planet,
    ColonyRequirements Requirements,
    PlanetAnalysis PlanetAnalysis
);

public record SynthesizePlanInput(
    string PlanetId,
    List<StructurePlan> StructurePlans
);
```

### Step 4: Orchestrator Activities

> **Note**: All orchestrator and worker activities use `DaprConversationClient` with constructor injection:
> ```csharp
> using Dapr.AI.Conversation;
> using Dapr.AI.Conversation.Extensions;
> using Dapr.AI.Conversation.ConversationRoles;
> 
> private readonly DaprConversationClient _conversationClient;
> 
> public YourActivity(DaprConversationClient conversationClient)
> {
>     _conversationClient = conversationClient;
> }
> ```
> Use `ConversationOptions("conversation")` with temperature (no model specification), then `ConverseAsync()` with message collections.

**AnalyzePlanetActivity.cs**
```csharp
using Dapr.AI.Conversation;
using Dapr.AI.Conversation.Extensions;
using Dapr.AI.Conversation.ConversationRoles;
using System.Text.Json;

public class AnalyzePlanetActivity : WorkflowActivity<Planet, PlanetAnalysis>
{
    private readonly DaprConversationClient _conversationClient;
    
    public AnalyzePlanetActivity(DaprConversationClient conversationClient)
    {
        _conversationClient = conversationClient;
    }
    
    public override async Task<PlanetAnalysis> RunAsync(
        WorkflowActivityContext context, 
        Planet input)
    {
        var options = new ConversationOptions("conversation")
        {
            Temperature = 0.7f
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
            }
        };
        
        var response = await _conversationClient.ConverseAsync(
            [
                new ConversationInput(new List<IConversationMessage>
                {
                    new SystemMessage
                    {
                        Content = [
                            new MessageContent(@"You are a planetary colonization expert. Analyze planets for 
                    colonization challenges and opportunities. Consider:
                    - Environmental hazards (radiation, temperature, atmosphere)
                    - Available resources
                    - Engineering requirements
                    - Long-term sustainability
                    
                    Respond **only** with valid JSON.
                    Do not include explanations, comments, or text outside the JSON object.
                    Ensure the JSON is syntactically correct and can be parsed without errors.
                    Use double quotes around all keys and string values.
                    Use opening and closing curly braces.

                    JSON structure that describes the fields:
                    {
                      ""challenges"": [""<challenge1>"", ""<challenge2>""],
                      ""opportunities"": [""<opportunity1>"", ""<opportunity2>""],
                      ""recommendedApproach"": ""<approach description>""
                    }

                    Example:
                    {
                      ""challenges"": [""High radiation levels require heavy shielding"", ""Thin atmosphere needs pressurized habitats""],
                      ""opportunities"": [""Rich metal deposits enable local manufacturing"", ""Low gravity reduces structural requirements""],
                      ""recommendedApproach"": ""Focus on underground construction to leverage natural radiation protection while exploiting mineral resources for building materials.""
                    }")
                        ]
                    },
                    new UserMessage
                    {
                        Name = "PlanetaryAnalyst",
                        Content = [
                            new MessageContent($@"Analyze planet for colonization:
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
- Soil: {input.Resources.SoilQuality}")
                        ]
                    }
                })
            ],
            options);
        
        var json = JsonSerializer.Deserialize<JsonElement>(
            response.Outputs.First().Choices.First().Message.Content);
        
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
using Dapr.AI.Conversation;
using Dapr.AI.Conversation.Extensions;
using Dapr.AI.Conversation.ConversationRoles;
using System.Text.Json;

public class DetermineStructuresActivity : WorkflowActivity<DetermineStructuresInput, List<StructureRequest>>
{
    private readonly DaprConversationClient _conversationClient;
    
    public DetermineStructuresActivity(DaprConversationClient conversationClient)
    {
        _conversationClient = conversationClient;
    }
    
    public override async Task<List<StructureRequest>> RunAsync(
        WorkflowActivityContext context, 
        DetermineStructuresInput input)
    {
        var options = new ConversationOptions("conversation")
        {
            Temperature = 0.7f
        };
        
        var response = await _conversationClient.ConverseAsync(
            [
                new ConversationInput(new List<IConversationMessage>
                {
                    new SystemMessage
                    {
                        Content = [
                            new MessageContent(@"You are a colony planning AI. Determine what structures are 
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
                    
                    Respond **only** with valid JSON.
                    Do not include explanations, comments, or text outside the JSON object.
                    Ensure the JSON is syntactically correct and can be parsed without errors.
                    Use double quotes around all keys and string values.
                    Use opening and closing curly braces.
                    
                    JSON structure that describes the fields:
                    {
                      ""structures"": [
                        {
                          ""structureType"": ""<HabitatDome|PowerPlant|Agriculture|MiningFacility|ResearchLab|DefenseSystem>"",
                          ""priority"": ""<Critical|High|Medium|Low>"",
                          ""quantity"": <number>,
                          ""reasoning"": ""<explanation>""
                        }
                      ]
                    }

                    Example:
                    {
                      ""structures"": [
                        {
                          ""structureType"": ""HabitatDome"",
                          ""priority"": ""Critical"",
                          ""quantity"": 3,
                          ""reasoning"": ""Initial population of 100 requires multiple habitat modules for redundancy and growth capacity""
                        },
                        {
                          ""structureType"": ""PowerPlant"",
                          ""priority"": ""Critical"",
                          ""quantity"": 2,
                          ""reasoning"": ""Dual power generation for reliability in harsh environment""
                        }
                      ]
                    }")
                        ]
                    },
                    new UserMessage
                    {
                        Name = "ColonyPlanner",
                        Content = [
                            new MessageContent($@"Determine structures needed:

Planet Challenges: {string.Join(\", \", input.PlanetAnalysis.Challenges)}
Planet Opportunities: {string.Join(\", \", input.PlanetAnalysis.Opportunities)}

Colony Requirements:
- Population: {input.Requirements.InitialPopulation} → {input.Requirements.TargetPopulation}
- Purpose: {input.Requirements.Purpose}
- Timeline: {input.Requirements.YearsToComplete} years

What structures are needed?")
                        ]
                    }
                })
            ],
            options);
        
        var json = JsonSerializer.Deserialize<JsonElement>(
            response.Outputs.First().Choices.First().Message.Content);
        
        var structures = json.GetProperty("structures");
        var result = new List<StructureRequest>();
        
        foreach (var item in structures.EnumerateArray())
        {
            result.Add(new StructureRequest(
                item.GetProperty("structureType").GetString(),
                Enum.Parse<Priority>(item.GetProperty("priority").GetString()),
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
using Dapr.AI.Conversation;
using Dapr.AI.Conversation.Extensions;
using Dapr.AI.Conversation.ConversationRoles;
using System.Text.Json;

public class PlanHabitatDomeActivity : WorkflowActivity<WorkerInput, StructurePlan>
{
    private readonly DaprConversationClient _conversationClient;
    
    public PlanHabitatDomeActivity(DaprConversationClient conversationClient)
    {
        _conversationClient = conversationClient;
    }
    
    public override async Task<StructurePlan> RunAsync(
        WorkflowActivityContext context, 
        WorkerInput input)
    {
        var options = new ConversationOptions("conversation")
        {
            Temperature = 0.7
        };
        
        var response = await _conversationClient.ConverseAsync(
            [
                new ConversationInput(new List<IConversationMessage>
                {
                    new SystemMessage
                    {
                        Content = [
                            new MessageContent(@"You are a habitat dome construction specialist. Design detailed 
                    plans for pressurized living environments. Consider:
                    - Atmospheric containment for planet conditions
                    - Radiation shielding requirements
                    - Life support systems
                    - Living space per person
                    - Emergency backup systems
                    
                    Respond **only** with valid JSON.
                    Do not include explanations, comments, or text outside the JSON object.
                    Ensure the JSON is syntactically correct and can be parsed without errors.
                    Use double quotes around all keys and string values.
                    Use opening and closing curly braces.
                    
                    JSON structure that describes the fields:
                    {
                      ""structureType"": ""<structure type>"",
                      ""quantity"": <number>,
                      ""materials"": [""<material1>"", ""<material2>""],
                      ""constructionDays"": <number>,
                      ""workerHours"": <number>,
                      ""prerequisites"": [""<prerequisite1>"", ""<prerequisite2>""],
                      ""detailedSpecification"": ""<full specification text>""
                    }

                    Example:
                    {
                      ""structureType"": ""HabitatDome"",
                      ""quantity"": 3,
                      ""materials"": [""Reinforced titanium-alloy framework"", ""Multi-layer radiation shielding"", ""Transparent aluminum viewports"", ""Life support modules""],
                      ""constructionDays"": 180,
                      ""workerHours"": 12000,
                      ""prerequisites"": [""Level foundation"", ""Power distribution network"", ""Water supply connection""],
                      ""detailedSpecification"": ""Each habitat dome provides 500 cubic meters of living space with triple-redundant life support, radiation shielding rated for 2 Sv/year environments, and emergency atmosphere retention systems. Modular design allows expansion...""
                    }")
                        ]
                    },
                    new UserMessage
                    {
                        Name = "HabitatDomeSpecialist",
                        Content = [
                            new MessageContent($@"Plan habitat domes:
Quantity Needed: {input.Request.Quantity}
Priority: {input.Request.Priority}

Planet Conditions:
- Atmosphere: {input.Planet.Conditions.AtmosphereType}
- Radiation: {input.Planet.Conditions.RadiationLevel} Sv/year
- Temperature: {input.Planet.Conditions.Temperature}°C

Challenges: {string.Join(", ", input.Analysis.Challenges)}

Provide detailed construction plan.")
                        ]
                    }
                })
            ],
            options);
        
        return ParseStructurePlan(response.Outputs.First().Choices.First().Message.Content);
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
using Dapr.AI.Conversation;
using Dapr.AI.Conversation.Extensions;
using Dapr.AI.Conversation.ConversationRoles;
using System.Text.Json;

public class PlanPowerPlantActivity : WorkflowActivity<WorkerInput, StructurePlan>
{
    private readonly DaprConversationClient _conversationClient;
    
    public PlanPowerPlantActivity(DaprConversationClient conversationClient)
    {
        _conversationClient = conversationClient;
    }
    
    public override async Task<StructurePlan> RunAsync(
        WorkflowActivityContext context, 
        WorkerInput input)
    {
        var options = new ConversationOptions("conversation")
        {
            Temperature = 0.7
        };
        
        var response = await _conversationClient.ConverseAsync(
            [
                new ConversationInput(new List<IConversationMessage>
                {
                    new SystemMessage
                    {
                        Content = [
                            new MessageContent(@"You are a power generation specialist. Design power plants 
                    appropriate for planetary conditions. Options:
                    - Solar (if adequate sunlight)
                    - Nuclear (if uranium available)
                    - Geothermal (if volcanic activity)
                    - Fusion (most reliable but complex)
                    
                    Respond **only** with valid JSON.
                    Do not include explanations, comments, or text outside the JSON object.
                    Ensure the JSON is syntactically correct and can be parsed without errors.
                    Use double quotes around all keys and string values.
                    Use opening and closing curly braces.
                    
                    JSON structure that describes the fields:
                    {
                      ""structureType"": ""<structure type>"",
                      ""quantity"": <number>,
                      ""materials"": [""<material1>"", ""<material2>""],
                      ""constructionDays"": <number>,
                      ""workerHours"": <number>,
                      ""prerequisites"": [""<prerequisite1>"", ""<prerequisite2>""],
                      ""detailedSpecification"": ""<full specification text>""
                    }

                    Example:
                    {
                      ""structureType"": ""PowerPlant"",
                      ""quantity"": 2,
                      ""materials"": [""Fusion reactor cores"", ""Plasma containment vessels"", ""Power distribution transformers"", ""Cooling systems""],
                      ""constructionDays"": 240,
                      ""workerHours"": 18000,
                      ""prerequisites"": [""Secure foundation with seismic dampening"", ""Coolant water supply"", ""Emergency shutdown systems""],
                      ""detailedSpecification"": ""Dual fusion reactors each generating 500MW, with automatic load balancing and failover capability. Tritium-deuterium fuel requires minimal resupply. Redundant cooling loops prevent meltdown scenarios...""
                    }")
                        ]
                    },
                    new UserMessage
                    {
                        Name = "PowerGenerationSpecialist",
                        Content = [
                            new MessageContent($@"Plan power generation:
Quantity: {input.Request.Quantity}

Planet:
- Day Length: {input.Planet.Conditions.DayLength} Earth days
- Uranium Available: {input.Planet.Resources.Uranium}
- Temperature: {input.Planet.Conditions.Temperature}°C

Design appropriate power plant(s).")
                        ]
                    }
                })
            ],
            options);
        
        return ParseStructurePlan(response.Outputs.First().Choices.First().Message.Content);
    }
}
```

**PlanAgricultureActivity.cs**
```csharp
using Dapr.AI.Conversation;
using Dapr.AI.Conversation.Extensions;
using Dapr.AI.Conversation.ConversationRoles;
using System.Text.Json;

public class PlanAgricultureActivity : WorkflowActivity<WorkerInput, StructurePlan>
{
    private readonly DaprConversationClient _conversationClient;
    
    public PlanAgricultureActivity(DaprConversationClient conversationClient)
    {
        _conversationClient = conversationClient;
    }
    
    public override async Task<StructurePlan> RunAsync(
        WorkflowActivityContext context, 
        WorkerInput input)
    {
        var conversationOptions = new ConversationOptions("conversation")
        {
            Temperature = 0.7
        };
        
        var response = await _conversationClient.ConverseAsync(
            [
                new ConversationInput(new List<IConversationMessage>
                {
                    new SystemMessage
                    {
                        Content = [
                            new MessageContent(@"You are an agricultural engineer specializing in off-world 
                    farming. Design food production facilities considering:
                    - Hydroponic vs soil-based farming
                    - Artificial lighting needs
                    - Water recycling systems
                    - Calorie production per person
                    
                    Respond **only** with valid JSON.
                    Do not include explanations, comments, or text outside the JSON object.
                    Ensure the JSON is syntactically correct and can be parsed without errors.
                    Use double quotes around all keys and string values.
                    Use opening and closing curly braces.
                    
                    JSON structure that describes the fields:
                    {
                      ""structureType"": ""<structure type>"",
                      ""quantity"": <number>,
                      ""materials"": [""<material1>"", ""<material2>""],
                      ""constructionDays"": <number>,
                      ""workerHours"": <number>,
                      ""prerequisites"": [""<prerequisite1>"", ""<prerequisite2>""],
                      ""detailedSpecification"": ""<full specification text>""
                    }

                    Example:
                    {
                      ""structureType"": ""Agriculture"",
                      ""quantity"": 4,
                      ""materials"": [""Hydroponic growth chambers"", ""LED grow lights"", ""Water recirculation systems"", ""Climate control units"", ""Seed vault""],
                      ""constructionDays"": 120,
                      ""workerHours"": 8000,
                      ""prerequisites"": [""Power supply connection"", ""Water purification system"", ""Atmospheric CO2 scrubbing""],
                      ""detailedSpecification"": ""Vertical farming towers with 12 levels each, producing 3000 calories per person per day through rotation of fast-growing crops. Automated nutrient delivery and harvesting systems minimize labor requirements...""
                    }")
                        ]
                    },
                    new UserMessage
                    {
                        Name = "AgriculturalEngineer",
                        Content = [
                            new MessageContent($@"Plan agricultural facilities:
Population: will grow to target size
Quantity: {input.Request.Quantity}

Planet:
- Water Available: {input.Planet.Resources.Water}
- Soil Quality: {input.Planet.Resources.SoilQuality}
- Organics: {input.Planet.Resources.Organics}

Design food production system.")
                        ]
                    }
                })
            ],
            options);
        
        return ParseStructurePlan(response.Outputs.First().Choices.First().Message.Content);
    }
}
```

### Step 6: Synthesis Activity
**SynthesizePlanActivity.cs**
```csharp
using Dapr.AI.Conversation;
using Dapr.AI.Conversation.Extensions;
using Dapr.AI.Conversation.ConversationRoles;
using System.Text.Json;

public class SynthesizePlanActivity : WorkflowActivity<SynthesizePlanInput, ColonyMasterPlan>
{
    private readonly DaprConversationClient _conversationClient;
    
    public SynthesizePlanActivity(DaprConversationClient conversationClient)
    {
        _conversationClient = conversationClient;
    }
    
    public override async Task<ColonyMasterPlan> RunAsync(
        WorkflowActivityContext context, 
        SynthesizePlanInput input)
    {
        // Aggregate materials
        var allMaterials = new Dictionary<string, int>();
        foreach (var plan in input.StructurePlans)
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
        var options = new ConversationOptions("conversation")
        {
            Temperature = 0.7
        };
        
        var response = await _conversationClient.ConverseAsync(
            [
                new ConversationInput(new List<IConversationMessage>
                {
                    new SystemMessage
                    {
                        Content = [
                            new MessageContent(@"You are a construction project manager. Organize structures 
                    into construction phases based on prerequisites and efficiency. 
                    
                    Respond **only** with valid JSON.
                    Do not include explanations, comments, or text outside the JSON object.
                    Ensure the JSON is syntactically correct and can be parsed without errors.
                    Use double quotes around all keys and string values.
                    Use opening and closing curly braces.
                    
                    JSON structure that describes the fields:
                    {
                      ""timeline"": [
                        {
                          ""phaseNumber"": <number>,
                          ""name"": ""<phase name>"",
                          ""structures"": [""<structure1>"", ""<structure2>""],
                          ""durationDays"": <number>
                        }
                      ],
                      ""successFactors"": ""<success factors description>"",
                      ""riskAssessment"": ""<risk assessment description>""
                    }

                    Example:
                    {
                      ""timeline"": [
                        {
                          ""phaseNumber"": 1,
                          ""name"": ""Foundation Infrastructure"",
                          ""structures"": [""PowerPlant"", ""Water Processing""],
                          ""durationDays"": 90
                        },
                        {
                          ""phaseNumber"": 2,
                          ""name"": ""Life Support Systems"",
                          ""structures"": [""HabitatDome"", ""Agriculture""],
                          ""durationDays"": 150
                        }
                      ],
                      ""successFactors"": ""Critical success depends on reliable power generation from day one, establishing closed-loop water recycling, and achieving food self-sufficiency within 6 months."",
                      ""riskAssessment"": ""Primary risks include construction delays due to extreme temperatures, potential equipment failures in high-radiation environment, and dependency on imported materials for first 2 years.""
                    }")
                        ]
                    },
                    new UserMessage
                    {
                        Name = "ProjectManager",
                        Content = [
                            new MessageContent($@"Organize construction timeline:
Structures: {JsonSerializer.Serialize(input.StructurePlans)}

Create phased construction plan.")
                        ]
                    }
                })
            ],
            options);
        
        var json = JsonSerializer.Deserialize<JsonElement>(
            response.Outputs.First().Choices.First().Message.Content);
        
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
using Dapr.AI.Conversation.Extensions;
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
