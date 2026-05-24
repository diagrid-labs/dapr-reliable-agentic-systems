using Dapr.AI.Conversation;
using Dapr.AI.Conversation.ConversationRoles;
using Dapr.AI.Conversation.Extensions;
using Dapr.Workflow;
using GalacticAnomalyClassifier.Models;
using Google.Protobuf.WellKnownTypes;
using System.Text.Json;

namespace GalacticAnomalyClassifier.Activities;

public record TemporalAnalysis(
    string Analysis,
    Dictionary<string, object> QuantumMetrics,
    List<string> SafetyProtocols,
    string TimelineStability
);

public class AnalyzeTemporalRiftActivity : WorkflowActivity<SpaceAnomaly, TemporalAnalysis>
{
    private readonly DaprConversationClient _conversationClient;
    
    public AnalyzeTemporalRiftActivity(DaprConversationClient conversationClient)
    {
        _conversationClient = conversationClient;
    }
    
    public override async Task<TemporalAnalysis> RunAsync(
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
                            new MessageContent(@"You are a quantum chronodynamics specialist. Analyze temporal 
                            rifts for:
                            - Timeline divergence probability
                            - Causality violation risk
                            - Temporal radiation levels
                            - Safe approach vectors
                            - Potential for time travel research

                            JSON structure that describes the fields:
                            {
                              ""analysis"": ""<detailed technical analysis>"",
                              ""quantumMetrics"": ""<JSON-encoded string of a dictionary<string, string> with relevant quantum metrics, use scientific E notation where necessary (for example 1.5e-35)>"",
                              ""safetyProtocols"": [""<safety protocol>""],
                              ""timelineStability"": ""<LOW, MEDIUM, HIGH, CRITICAL>""
                            }
                            ")
                        ]
                    },
                    new UserMessage
                    {
                        Name = "TemporalAnalyst",
                        Content = [
                            new MessageContent($"Analyze temporal rift: {input.SensorData}")
                        ]
                    }
                })
            ],
            conversationOptions);
        
        var json = JsonSerializer.Deserialize<JsonElement>(
            response.Outputs.First().Choices.First().Message.Content);
        
        return new TemporalAnalysis(
            json.GetProperty("analysis").GetString()!,
            JsonSerializer.Deserialize<Dictionary<string, object>>(
                json.GetProperty("quantumMetrics").GetString()!)!,
            JsonSerializer.Deserialize<List<string>>(
                json.GetProperty("safetyProtocols").GetRawText())!,
            json.GetProperty("timelineStability").GetString()!
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
        properties.Fields.Add("quantumMetrics", Value.ForStruct(stringType));
        properties.Fields.Add("safetyProtocols", Value.ForStruct(stringArrayType));
        properties.Fields.Add("timelineStability", Value.ForStruct(stringType));

        var responseFormat = new Struct();
        responseFormat.Fields.Add("type", Value.ForString("object"));
        responseFormat.Fields.Add("properties", Value.ForStruct(properties));
        responseFormat.Fields.Add("required", Value.ForList(
            Value.ForString("analysis"),
            Value.ForString("quantumMetrics"),
            Value.ForString("safetyProtocols"),
            Value.ForString("timelineStability")));

        return responseFormat;
    }
}
