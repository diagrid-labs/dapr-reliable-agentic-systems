using Dapr.AI.Conversation;
using Dapr.AI.Conversation.ConversationRoles;
using Dapr.AI.Conversation.Extensions;
using Dapr.Workflow;
using GalacticAnomalyClassifier.Models;
using System.Text.Json;

namespace GalacticAnomalyClassifier.Activities;

public record DarkMatterAnalysis(
    string Analysis,
    Dictionary<string, object> GravitationalData,
    List<string> HarvestingOpportunities,
    string CollapseProbability
);

public class AnalyzeDarkMatterActivity : WorkflowActivity<SpaceAnomaly, DarkMatterAnalysis>
{
    private readonly DaprConversationClient _conversationClient;
    
    public AnalyzeDarkMatterActivity(DaprConversationClient conversationClient)
    {
        _conversationClient = conversationClient;
    }
    
    public override async Task<DarkMatterAnalysis> RunAsync(
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
                            new MessageContent(@"You are a dark matter physicist. Analyze dark matter clusters for:
                            - Mass concentration and distribution
                            - Gravitational lensing effects
                            - Exotic matter harvesting potential
                            - Black hole formation risk
                            - Energy extraction possibilities
                            
                            The response should be JSON. Do not start the response with any preamble or formatting instructions. Do not wrap the response in a markdown codeblock for json.
                            Respond only in JSON format as follows:
                            {
                              ""analysis"": ""<detailed technical analysis of the dark matter cluster>"",
                              ""gravitationalData"": <A dictionary<string, double> with relevant gravitational data>,
                              ""harvestingOpportunities"": ""<list of harvesting opportunities>"",
                              ""collapseProbability"": ""<LOW, MEDIUM, HIGH, CRITICAL>""
                            }
                            ")
                        ]
                    },
                    new UserMessage
                    {
                        Name = "DarkMatterPhysicist",
                        Content = [
                            new MessageContent($"Analyze dark matter cluster: {input.SensorData}")
                        ]
                    }
                })
            ],
            conversationOptions);
        
        Console.WriteLine($"Analyze Dark Matter Response: {response.Outputs.First().Choices.First().Message.Content}");

        var json = JsonSerializer.Deserialize<JsonElement>(
            response.Outputs.First().Choices.First().Message.Content);
        
        return new DarkMatterAnalysis(
            json.GetProperty("analysis").GetString()!,
            JsonSerializer.Deserialize<Dictionary<string, object>>(
                json.GetProperty("gravitationalData").GetRawText())!,
            JsonSerializer.Deserialize<List<string>>(
                json.GetProperty("harvestingOpportunities").GetRawText())!,
            json.GetProperty("collapseProbability").GetString()!
        );
    }
}
