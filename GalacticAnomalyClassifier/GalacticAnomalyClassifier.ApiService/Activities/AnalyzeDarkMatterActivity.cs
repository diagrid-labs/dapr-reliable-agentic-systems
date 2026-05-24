using Dapr.AI.Conversation;
using Dapr.AI.Conversation.ConversationRoles;
using Dapr.AI.Conversation.Extensions;
using Dapr.Workflow;
using GalacticAnomalyClassifier.Models;
using Google.Protobuf.WellKnownTypes;
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
                            new MessageContent(@"You are a dark matter physicist. Analyze dark matter clusters for:
                            - Mass concentration and distribution
                            - Gravitational lensing effects
                            - Exotic matter harvesting potential
                            - Black hole formation risk
                            - Energy extraction possibilities

                            JSON structure that describes the fields:
                            {
                              ""analysis"": ""<detailed technical analysis of the dark matter cluster>"",
                              ""gravitationalData"": ""<JSON-encoded string of a dictionary<string, string> with relevant gravitational data, use scientific E notation where necessary (for example 1.5e-35)>"",
                              ""harvestingOpportunities"": [""<harvesting opportunity>""],
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
        
        var json = JsonSerializer.Deserialize<JsonElement>(
            response.Outputs.First().Choices.First().Message.Content);
        
        return new DarkMatterAnalysis(
            json.GetProperty("analysis").GetString()!,
            JsonSerializer.Deserialize<Dictionary<string, object>>(
                json.GetProperty("gravitationalData").GetString()!)!,
            JsonSerializer.Deserialize<List<string>>(
                json.GetProperty("harvestingOpportunities").GetRawText())!,
            json.GetProperty("collapseProbability").GetString()!
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
        properties.Fields.Add("gravitationalData", Value.ForStruct(stringType));
        properties.Fields.Add("harvestingOpportunities", Value.ForStruct(stringArrayType));
        properties.Fields.Add("collapseProbability", Value.ForStruct(stringType));

        var responseFormat = new Struct();
        responseFormat.Fields.Add("type", Value.ForString("object"));
        responseFormat.Fields.Add("properties", Value.ForStruct(properties));
        responseFormat.Fields.Add("required", Value.ForList(
            Value.ForString("analysis"),
            Value.ForString("gravitationalData"),
            Value.ForString("harvestingOpportunities"),
            Value.ForString("collapseProbability")));

        return responseFormat;
    }
}
