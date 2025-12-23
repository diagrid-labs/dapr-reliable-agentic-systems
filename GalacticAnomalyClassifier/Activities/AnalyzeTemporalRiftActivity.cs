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
                            
                            Respond **only** with valid JSON.
                            Do not include explanations, comments, or text outside the JSON object.
                            Ensure the JSON is syntactically correct and can be parsed without errors.
                            Use double quotes around all keys and string values.
                            Use opening and closing curly braces.

                            JSON structure that describes the fields:
                            {
                              ""analysis"": ""<detailed technical analysis>"",
                              ""quantumMetrics"": <A dictionary<string, string> with relevant quantum metrics, use scientific E notation where necessary (for example 1.5e-35)>,
                              ""safetyProtocols"": ""<list of safety protocols>"",
                              ""timelineStability"": ""<LOW, MEDIUM, HIGH, CRITICAL>""
                            }

                            Example:
                            {
                              ""analysis"": ""The temporal rift exhibits moderate timeline divergence with potential causality violations..."",
                              ""quantumMetrics"": { ""divergenceIndex"": ""2.3e1"", ""radiationLevel"": ""5.6e-4 Sv/h"" },
                              ""safetyProtocols"": [""Establish temporal anchors"", ""Deploy chronal dampeners""],
                              ""timelineStability"": ""MEDIUM""
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
