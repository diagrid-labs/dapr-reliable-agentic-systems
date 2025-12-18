using Dapr.AI.Conversation;
using Dapr.AI.Conversation.ConversationRoles;
using Dapr.AI.Conversation.Extensions;
using Dapr.Workflow;
using GalacticAnomalyClassifier.Models;
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
            Temperature = 0.7
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
                            
                            The response should be JSON, not markdown. Do not start the response with any preamble or formatting instructions.
                            Respond only in JSON format as follows:
                            {
                              ""analysis"": ""<detailed technical analysis of the stellar event>"",
                              ""astrophysicsData"": <A dictionary<string, string> with relevant astrophysics data, use scientific E notation where necessary (for example 1.5e-35)>,
                              ""observationProtocols"": ""<list of observation protocols>"",
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
        
        Console.WriteLine($"Analyze Stellar Phenomenon Response: {response.Outputs.First().Choices.First().Message.Content}");

        var json = JsonSerializer.Deserialize<JsonElement>(
            response.Outputs.First().Choices.First().Message.Content);
        
        return new StellarAnalysis(
            json.GetProperty("analysis").GetString()!,
            JsonSerializer.Deserialize<Dictionary<string, object>>(
                json.GetProperty("astrophysicsData").GetRawText())!,
            JsonSerializer.Deserialize<List<string>>(
                json.GetProperty("observationProtocols").GetRawText())!,
            json.GetProperty("radiationLevel").GetString()!
        );
    }
}
