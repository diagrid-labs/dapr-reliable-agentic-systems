using Dapr.AI.Conversation;
using Dapr.AI.Conversation.ConversationRoles;
using Dapr.AI.Conversation.Extensions;
using Dapr.Workflow;
using GalacticAnomalyClassifier.Models;
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
            Temperature = 0.7
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
                            
                            The response should be JSON. Do not start the response with any preamble or formatting instructions. Do not wrap the response in a markdown codeblock for json.
                            Respond only in JSON format as follows:
                            {
                              ""analysis"": ""<detailed technical analysis>"",
                              ""quantumMetrics"": <A dictionary<string, double> with relevant quantum metrics>,
                              ""safetyProtocols"": ""<list of safety protocols>"",
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
        
        Console.WriteLine($"Analyze Temporal Rift Response: {response.Outputs.First().Choices.First().Message.Content}");

        var json = JsonSerializer.Deserialize<JsonElement>(
            response.Outputs.First().Choices.First().Message.Content);
        
        return new TemporalAnalysis(
            json.GetProperty("analysis").GetString()!,
            JsonSerializer.Deserialize<Dictionary<string, object>>(
                json.GetProperty("quantumMetrics").GetRawText())!,
            JsonSerializer.Deserialize<List<string>>(
                json.GetProperty("safetyProtocols").GetRawText())!,
            json.GetProperty("timelineStability").GetString()!
        );
    }
}
