using Dapr.AI.Conversation;
using Dapr.AI.Conversation.ConversationRoles;
using Dapr.AI.Conversation.Extensions;
using Dapr.Workflow;
using GalacticAnomalyClassifier.Models;
using System.Text.Json;

namespace GalacticAnomalyClassifier.Activities;

public record ArtifactAnalysis(
    string Analysis,
    Dictionary<string, object> XenoarchaeologyData,
    List<string> ExtractionProcedures,
    string HostilityIndicators
);

public class AnalyzeAlienArtifactActivity : WorkflowActivity<SpaceAnomaly, ArtifactAnalysis>
{
    private readonly DaprConversationClient _conversationClient;
    
    public AnalyzeAlienArtifactActivity(DaprConversationClient conversationClient)
    {
        _conversationClient = conversationClient;
    }
    
    public override async Task<ArtifactAnalysis> RunAsync(
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
                            new MessageContent(@"You are a xenoarchaeologist specializing in alien artifacts. Analyze for:
                            - Estimated age and civilization of origin
                            - Technology level and purpose
                            - Active vs dormant status
                            - Defensive mechanisms or traps
                            - Cultural and scientific value
                            - Safe extraction procedures
                            
                            The response should be JSON, not markdown. Do not start the response with any preamble or formatting instructions.
                            Respond only in JSON format as follows:
                            {
                              ""analysis"": ""<detailed technical analysis of the alien artifact>"",
                              ""xenoarchaeologyData"": <A dictionary<string, string> with relevant artifacts data, use scientific E notation where necessary (for example 1.5e-35)>,
                              ""extractionProcedures"": ""<list of extraction procedures>"",
                              ""hostilityIndicator"": ""<SAFE, CAUTION, DANGEROUS, LETHAL>""
                            }
                            ")
                        ]
                    },
                    new UserMessage
                    {
                        Name = "Xenoarchaeologist",
                        Content = [
                            new MessageContent($"Analyze alien artifact: {input.SensorData}")
                        ]
                    }
                })
            ],
            conversationOptions);
        
        Console.WriteLine($"Analyze Alien Artifact Response: {response.Outputs.First().Choices.First().Message.Content}");

        var json = JsonSerializer.Deserialize<JsonElement>(
            response.Outputs.First().Choices.First().Message.Content);
        
        return new ArtifactAnalysis(
            json.GetProperty("analysis").GetString()!,
            JsonSerializer.Deserialize<Dictionary<string, object>>(
                json.GetProperty("xenoarchaeologyData").GetRawText())!,
            JsonSerializer.Deserialize<List<string>>(
                json.GetProperty("extractionProcedures").GetRawText())!,
            json.GetProperty("hostilityIndicator").GetString()!
        );
    }
}
