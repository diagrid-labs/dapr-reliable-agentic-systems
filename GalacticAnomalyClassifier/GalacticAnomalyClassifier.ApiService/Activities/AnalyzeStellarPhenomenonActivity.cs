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
                            
                            Respond **only** with valid JSON.
                            Do not include explanations, comments, or text outside the JSON object.
                            Ensure the JSON is syntactically correct and can be parsed without errors.
                            Use double quotes around all keys and string values.
                            Use opening and closing curly braces.

                            JSON structure that describes the fields:
                            {
                              ""analysis"": ""<detailed technical analysis of the stellar event>"",
                              ""astrophysicsData"": <A dictionary<string, string> with relevant astrophysics data, use scientific E notation where necessary (for example 1.5e-35)>,
                              ""observationProtocols"": ""<list of observation protocols>"",
                              ""radiationLevel"": ""<LOW, MEDIUM, HIGH, CRITICAL>""
                            }

                            Example:
                            {
                              ""analysis"": ""The stellar phenomenon appears to be a Type II supernova with an estimated energy output of 1.0e44 joules..."",
                              ""astrophysicsData"": { ""peakLuminosity"": ""3.5e9 L☉"", ""radiationFlux"": ""2.1e-3 W/m²"" },
                              ""observationProtocols"": [""Maintain distance of at least 5 light-years"", ""Use gamma-ray detectors""],
                              ""radiationLevel"": ""HIGH""
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
