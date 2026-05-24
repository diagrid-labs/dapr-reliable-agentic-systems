using Dapr.AI.Conversation;
using Dapr.AI.Conversation.Extensions;
using Dapr.AI.Conversation.ConversationRoles;
using Dapr.Workflow;
using SpaceColonyPlanner.Models;
using System.Text.Json;

namespace SpaceColonyPlanner.Activities.Analysis;

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

Planet Challenges: {string.Join(", ", input.PlanetAnalysis.Challenges)}
Planet Opportunities: {string.Join(", ", input.PlanetAnalysis.Opportunities)}

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
                item.GetProperty("structureType").GetString()!,
                Enum.Parse<Priority>(item.GetProperty("priority").GetString()!),
                item.GetProperty("quantity").GetInt32(),
                item.GetProperty("reasoning").GetString()!
            ));
        }
        
        return result;
    }
}
