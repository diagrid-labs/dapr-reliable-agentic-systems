using Dapr.AI.Conversation;
using Dapr.AI.Conversation.ConversationRoles;
using Dapr.AI.Conversation.Extensions;
using Dapr.Workflow;
using GalacticAnomalyClassifier.Models;
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
            Temperature = 0.7
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
                            
                            Respond **only** with valid JSON.
                            Do not include explanations, comments, or text outside the JSON object.
                            Ensure the JSON is syntactically correct and can be parsed without errors.
                            Use double quotes around all keys and string values.
                            Use opening and closing curly braces.

                            JSON structure that describes the fields:
                            {
                              ""analysis"": ""<detailed technical analysis of the dimensional tear>"",
                              ""multiverseMetrics"": <A dictionary<string, string> with relevant multiverse data, use scientific E notation where necessary (for example 1.5e-35)>,
                              ""containmentProcedures"": ""<list of containment procedures>"",
                              ""spacetimeTearSeverity"": ""<LOW, MEDIUM, HIGH, CRITICAL>""
                            }

                            Example:
                            {
                              ""analysis"": ""The dimensional tear exhibits high instability with significant cross-dimensional contamination risk..."",
                              ""multiverseMetrics"": { ""tearSize"": ""3.2e3 meters"", ""contaminationLevel"": ""7.5e2 particles per cubic meter"" },
                              ""containmentProcedures"": [""Deploy quantum stabilizers"", ""Establish dimensional anchor points""],
                              ""spacetimeTearSeverity"": ""HIGH""
                            }
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
        
        Console.WriteLine($"Analyze Dimensional Tear Response: {response.Outputs.First().Choices.First().Message.Content}");

        var json = JsonSerializer.Deserialize<JsonElement>(
            response.Outputs.First().Choices.First().Message.Content);
        
        return new DimensionalAnalysis(
            json.GetProperty("analysis").GetString()!,
            JsonSerializer.Deserialize<Dictionary<string, object>>(
                json.GetProperty("multiverseMetrics").GetRawText())!,
            JsonSerializer.Deserialize<List<string>>(
                json.GetProperty("containmentProcedures").GetRawText())!,
            json.GetProperty("spacetimeTearSeverity").GetString()!
        );
    }
}
