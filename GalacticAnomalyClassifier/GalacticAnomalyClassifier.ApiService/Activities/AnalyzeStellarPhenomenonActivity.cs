using Dapr.AI.Conversation;
using Dapr.AI.Conversation.ConversationRoles;
using Dapr.AI.Conversation.Extensions;
using Dapr.Workflow;
using GalacticAnomalyClassifier.Models;
using Google.Protobuf.WellKnownTypes;
using System.Text.Json;

namespace GalacticAnomalyClassifier.Activities;

public record StellarAnalysis(
    string Analysis,
    Dictionary<string, object> AstrophysicsData,
    List<string> ObservationProtocols,
    string RadiationLevel
);

public class AnalyzeStellarPhenomenonActivity : WorkflowActivity<SpaceAnomaly, StellarAnalysis>
{
    private readonly DaprConversationClient _conversationClient;
    
    public AnalyzeStellarPhenomenonActivity(DaprConversationClient conversationClient)
    {
        _conversationClient = conversationClient;
    }
    
    public override async Task<StellarAnalysis> RunAsync(
        WorkflowActivityContext context, 
        SpaceAnomaly input)
    {
        var conversationOptions = new ConversationOptions("conversation")
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
                            new MessageContent(@"You are an astrophysicist specializing in stellar phenomena. Analyze for:
                            - Type of stellar event (supernova, neutron star, etc.)
                            - Energy output and radiation levels
                            - Impact on surrounding space
                            - Scientific observation opportunities
                            - Safe observation distance
                            - Duration and evolution predictions

                            JSON structure that describes the fields:
                            {
                              ""analysis"": ""<detailed technical analysis of the stellar event>"",
                              ""astrophysicsData"": ""<JSON-encoded string of a dictionary<string, string> with relevant astrophysics data, use scientific E notation where necessary (for example 1.5e-35)>"",
                              ""observationProtocols"": [""<observation protocol>""],
                              ""radiationLevel"": ""<LOW, MEDIUM, HIGH, CRITICAL>""
                            }
                            ")
                        ]
                    },
                    new UserMessage
                    {
                        Name = "Astrophysicist",
                        Content = [
                            new MessageContent($"Analyze stellar phenomenon: {input.SensorData}")
                        ]
                    }
                })
            ],
            conversationOptions);
        
        var json = JsonSerializer.Deserialize<JsonElement>(
            response.Outputs.First().Choices.First().Message.Content);
        
        return new StellarAnalysis(
            json.GetProperty("analysis").GetString()!,
            JsonSerializer.Deserialize<Dictionary<string, object>>(
                json.GetProperty("astrophysicsData").GetString()!)!,
            JsonSerializer.Deserialize<List<string>>(
                json.GetProperty("observationProtocols").GetRawText())!,
            json.GetProperty("radiationLevel").GetString()!
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
        properties.Fields.Add("analysis", Value.ForStruct(stringType));
        properties.Fields.Add("astrophysicsData", Value.ForStruct(stringType));
        properties.Fields.Add("observationProtocols", Value.ForStruct(stringArrayType));
        properties.Fields.Add("radiationLevel", Value.ForStruct(stringType));

        var responseFormat = new Struct();
        responseFormat.Fields.Add("type", Value.ForString("object"));
        responseFormat.Fields.Add("properties", Value.ForStruct(properties));
        responseFormat.Fields.Add("required", Value.ForList(
            Value.ForString("analysis"),
            Value.ForString("astrophysicsData"),
            Value.ForString("observationProtocols"),
            Value.ForString("radiationLevel")));

        return responseFormat;
    }
}
