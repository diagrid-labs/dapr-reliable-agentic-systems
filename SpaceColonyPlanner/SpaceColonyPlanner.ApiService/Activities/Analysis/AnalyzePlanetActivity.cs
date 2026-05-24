using Dapr.AI.Conversation;
using Dapr.AI.Conversation.Extensions;
using Dapr.AI.Conversation.ConversationRoles;
using Dapr.Workflow;
using SpaceColonyPlanner.Models;
using System.Text.Json;

namespace SpaceColonyPlanner.Activities.Analysis;

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
            Temperature = 0.7
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
                json.GetProperty("challenges").GetRawText())!,
            JsonSerializer.Deserialize<List<string>>(
                json.GetProperty("opportunities").GetRawText())!,
            json.GetProperty("recommendedApproach").GetString()!
        );
    }
}
