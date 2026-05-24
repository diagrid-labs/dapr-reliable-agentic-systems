using Dapr.AI.Conversation;
using Dapr.AI.Conversation.Extensions;
using Dapr.AI.Conversation.ConversationRoles;
using Dapr.Workflow;
using Google.Protobuf.WellKnownTypes;
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
            Temperature = 0.7,
            ResponseFormat = GetResponseFormat()
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

                    JSON structure that describes the fields:
                    {
                      ""challenges"": [""<challenge1>"", ""<challenge2>""],
                      ""opportunities"": [""<opportunity1>"", ""<opportunity2>""],
                      ""recommendedApproach"": ""<approach description>""
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

    private static Struct GetResponseFormat()
    {
        var stringType = new Struct();
        stringType.Fields.Add("type", Value.ForString("string"));

        var stringArrayType = new Struct();
        stringArrayType.Fields.Add("type", Value.ForString("array"));
        stringArrayType.Fields.Add("items", Value.ForStruct(stringType));

        var properties = new Struct();
        properties.Fields.Add("challenges", Value.ForStruct(stringArrayType));
        properties.Fields.Add("opportunities", Value.ForStruct(stringArrayType));
        properties.Fields.Add("recommendedApproach", Value.ForStruct(stringType));

        var responseFormat = new Struct();
        responseFormat.Fields.Add("type", Value.ForString("object"));
        responseFormat.Fields.Add("properties", Value.ForStruct(properties));
        responseFormat.Fields.Add("required", Value.ForList(
            Value.ForString("challenges"),
            Value.ForString("opportunities"),
            Value.ForString("recommendedApproach")));

        return responseFormat;
    }
}
