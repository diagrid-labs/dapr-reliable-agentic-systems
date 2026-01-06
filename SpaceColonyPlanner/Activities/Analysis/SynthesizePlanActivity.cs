using Dapr.AI.Conversation;
using Dapr.AI.Conversation.Extensions;
using Dapr.AI.Conversation.ConversationRoles;
using Dapr.Workflow;
using SpaceColonyPlanner.Models;
using System.Text.Json;

namespace SpaceColonyPlanner.Activities.Analysis;

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
            Temperature = 0.7f
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
                phase.GetProperty("name").GetString()!,
                JsonSerializer.Deserialize<List<string>>(
                    phase.GetProperty("structures").GetRawText())!,
                phase.GetProperty("durationDays").GetInt32()
            ));
        }
        
        return new ColonyMasterPlan(
            input.PlanetId,
            input.StructurePlans,
            timeline.Sum(p => p.DurationDays),
            allMaterials,
            timeline,
            json.GetProperty("successFactors").GetString()!,
            json.GetProperty("riskAssessment").GetString()!
        );
    }
}
