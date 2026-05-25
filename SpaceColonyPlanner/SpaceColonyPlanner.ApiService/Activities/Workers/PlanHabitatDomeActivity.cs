using Dapr.AI.Conversation;
using Dapr.AI.Conversation.Extensions;
using Dapr.AI.Conversation.ConversationRoles;
using Dapr.Workflow;
using SpaceColonyPlanner.Models;
using System.Text.Json;

namespace SpaceColonyPlanner.Activities.Workers;

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
                            new MessageContent(@"You are a habitat dome construction specialist. Design detailed 
                    plans for pressurized living environments. Consider:
                    - Atmospheric containment for planet conditions
                    - Radiation shielding requirements
                    - Life support systems
                    - Living space per person
                    - Emergency backup systems

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

                    Keep `detailedSpecification` <=80 words. `materials` and `prerequisites` <=5 items each. Return only the JSON object with no surrounding prose.")
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
