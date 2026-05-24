using System.Text.Json;
using Dapr.AI.Conversation;
using Dapr.AI.Conversation.ConversationRoles;
using Dapr.AI.Conversation.Extensions;
using Dapr.Workflow;
using SpaceColonyPlanner.Models;

namespace SpaceColonyPlanner.Activities.Workers;

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
                            new MessageContent(@"You are a power generation specialist. Design power plants 
                    appropriate for planetary conditions. Options:
                    - Solar (if adequate sunlight)
                    - Nuclear (if uranium available)
                    - Geothermal (if volcanic activity)
                    - Fusion (most reliable but complex)

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
