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
                            
                            The response should be JSON, not markdown. Do not start the response with any preamble or formatting instructions.
                            Respond only in JSON format as follows:
                            {
                              ""analysis"": ""<detailed technical analysis of the dimensional tear>"",
                              ""multiverseMetrics"": <A dictionary<string, string> with relevant multiverse data, use scientific E notation where necessary (for example 1.5e-35)>,
                              ""containmentProcedures"": ""<list of containment procedures>"",
                              ""spacetimeTearSeverity"": ""<LOW, MEDIUM, HIGH, CRITICAL>""
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
