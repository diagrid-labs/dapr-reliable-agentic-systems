using Dapr.AI.Conversation;
using Dapr.AI.Conversation.ConversationRoles;
using Dapr.AI.Conversation.Extensions;
using Dapr.Workflow;
using GalacticAnomalyClassifier.Models;
using Google.Protobuf.WellKnownTypes;
using System.Text.Json;

namespace GalacticAnomalyClassifier.Activities;

public record DimensionalAnalysis(
    string Analysis,
    Dictionary<string, object> MultiverseMetrics,
    List<string> ContainmentProcedures,
    string RealityStability
);

public class AnalyzeDimensionalTearActivity : WorkflowActivity<SpaceAnomaly, DimensionalAnalysis>
{
    private readonly DaprConversationClient _conversationClient;
    
    public AnalyzeDimensionalTearActivity(DaprConversationClient conversationClient)
    {
        _conversationClient = conversationClient;
    }
    
    public override async Task<DimensionalAnalysis> RunAsync(
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
                            new MessageContent(@"You are a theoretical physicist specializing in multiverse theory. Analyze dimensional tears for:
                            - Reality breach severity
                            - Cross-dimensional contamination risk
                            - Parallel universe interaction probability
                            - Containment and sealing procedures
                            - Research and exploration potential
                            - Stability of local spacetime fabric

                            JSON structure that describes the fields:
                            {
                              ""analysis"": ""<detailed technical analysis of the dimensional tear>"",
                              ""multiverseMetrics"": ""<JSON-encoded string of a dictionary<string, string> with relevant multiverse data, use scientific E notation where necessary (for example 1.5e-35)>"",
                              ""containmentProcedures"": [""<containment procedure>""],
                              ""spacetimeTearSeverity"": ""<LOW, MEDIUM, HIGH, CRITICAL>""
                            }

                            Keep `analysis` <=80 words. Array fields <=5 items. Return only the JSON object with no surrounding prose.
                            ")
                        ]
                    },
                    new UserMessage
                    {
                        Name = "MultiversePhysicist",
                        Content = [
                            new MessageContent($"Analyze dimensional tear: {input.SensorData}")
                        ]
                    }
                })
            ],
            conversationOptions);
        
        var json = JsonSerializer.Deserialize<JsonElement>(
            response.Outputs.First().Choices.First().Message.Content);
        
        return new DimensionalAnalysis(
            json.GetProperty("analysis").GetString()!,
            JsonSerializer.Deserialize<Dictionary<string, object>>(
                json.GetProperty("multiverseMetrics").GetString()!)!,
            JsonSerializer.Deserialize<List<string>>(
                json.GetProperty("containmentProcedures").GetRawText())!,
            json.GetProperty("spacetimeTearSeverity").GetString()!
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
        properties.Fields.Add("multiverseMetrics", Value.ForStruct(stringType));
        properties.Fields.Add("containmentProcedures", Value.ForStruct(stringArrayType));
        properties.Fields.Add("spacetimeTearSeverity", Value.ForStruct(stringType));

        var responseFormat = new Struct();
        responseFormat.Fields.Add("type", Value.ForString("object"));
        responseFormat.Fields.Add("properties", Value.ForStruct(properties));
        responseFormat.Fields.Add("required", Value.ForList(
            Value.ForString("analysis"),
            Value.ForString("multiverseMetrics"),
            Value.ForString("containmentProcedures"),
            Value.ForString("spacetimeTearSeverity")));

        return responseFormat;
    }
}
