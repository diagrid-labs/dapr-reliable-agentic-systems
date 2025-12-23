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
                            
                            Respond **only** with valid JSON.
                            Do not include explanations, comments, or text outside the JSON object.
                            Ensure the JSON is syntactically correct and can be parsed without errors.
                            Use double quotes around all keys and string values.
                            Use opening and closing curly braces.

                            JSON structure that describes the fields:
                            {
                              ""analysis"": ""<detailed technical analysis of the dark matter cluster>"",
                              ""gravitationalData"": <A dictionary<string, string> with relevant gravitational data, use scientific E notation where necessary (for example 1.5e-35)>,
                              ""harvestingOpportunities"": ""<list of harvesting opportunities>"",
                              ""collapseProbability"": ""<LOW, MEDIUM, HIGH, CRITICAL>""
                            }

                            Example:
                            {
                              ""analysis"": ""The dark matter cluster exhibits a high mass concentration with significant gravitational lensing effects..."",
                              ""gravitationalData"": { ""massDensity"": ""2.5e10 solar masses per cubic parsec"", ""lensingEffect"": ""Strong"" },
                              ""harvestingOpportunities"": [""Exotic particle extraction"", ""Dark energy conversion""],
                              ""collapseProbability"": ""MEDIUM""
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
