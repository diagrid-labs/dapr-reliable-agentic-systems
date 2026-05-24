using Dapr.AI.Conversation;
using Dapr.AI.Conversation.Extensions;
using Dapr.AI.Conversation.ConversationRoles;
using Dapr.Workflow;
using SpaceColonyPlanner.Models;
using System.Text.Json;

namespace SpaceColonyPlanner.Activities.Workers;

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
            Temperature = 0.7f,
            ResponseFormat = StructurePlanSchema.Get()
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

                    JSON structure that describes the fields:
                    {
                      ""structureType"": ""<structure type>"",
                      ""quantity"": <number>,
                      ""materials"": [""<material1>"", ""<material2>""],
                      ""constructionDays"": <number>,
                      ""workerHours"": <number>,
                      ""prerequisites"": [""<prerequisite1>"", ""<prerequisite2>""],
                      ""detailedSpecification"": ""<full specification text>""
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
            conversationOptions);
        
        return ParseStructurePlan(response.Outputs.First().Choices.First().Message.Content);
    }
    
    private static StructurePlan ParseStructurePlan(string jsonContent)
    {
        var json = JsonSerializer.Deserialize<JsonElement>(jsonContent);
        return new StructurePlan(
            json.GetProperty("structureType").GetString()!,
            json.GetProperty("quantity").GetInt32(),
            JsonSerializer.Deserialize<List<string>>(
                json.GetProperty("materials").GetRawText())!,
            json.GetProperty("constructionDays").GetInt32(),
            json.GetProperty("workerHours").GetInt32(),
            JsonSerializer.Deserialize<List<string>>(
                json.GetProperty("prerequisites").GetRawText())!,
            json.GetProperty("detailedSpecification").GetString()!
        );
    }
}
