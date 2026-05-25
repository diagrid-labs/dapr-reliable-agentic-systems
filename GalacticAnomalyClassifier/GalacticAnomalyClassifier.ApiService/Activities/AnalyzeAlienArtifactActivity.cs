using Dapr.AI.Conversation;
using Dapr.AI.Conversation.ConversationRoles;
using Dapr.AI.Conversation.Extensions;
using Dapr.Workflow;
using GalacticAnomalyClassifier.Models;
using Google.Protobuf.WellKnownTypes;
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
                            new MessageContent(@"You are a xenoarchaeologist specializing in alien artifacts. Analyze for:
                            - Estimated age and civilization of origin
                            - Technology level and purpose
                            - Active vs dormant status
                            - Defensive mechanisms or traps
                            - Cultural and scientific value
                            - Safe extraction procedures

                            JSON structure that describes the fields:
                            {
                              ""analysis"": ""<detailed technical analysis of the alien artifact>"",
                              ""xenoarchaeologyData"": ""<JSON-encoded string of a dictionary<string, string> with relevant artifacts data, use scientific E notation where necessary (for example 1.5e-35)>"",
                              ""extractionProcedures"": [""<extraction procedure>""],
                              ""hostilityIndicator"": ""<SAFE, CAUTION, DANGEROUS, LETHAL>""
                            }

                            Keep `analysis` <=80 words. Array fields <=5 items. Return only the JSON object with no surrounding prose.
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
        
        var json = JsonSerializer.Deserialize<JsonElement>(
            response.Outputs.First().Choices.First().Message.Content);
        
        return new ArtifactAnalysis(
            json.GetProperty("analysis").GetString()!,
            JsonSerializer.Deserialize<Dictionary<string, object>>(
                json.GetProperty("xenoarchaeologyData").GetString()!)!,
            JsonSerializer.Deserialize<List<string>>(
                json.GetProperty("extractionProcedures").GetRawText())!,
            json.GetProperty("hostilityIndicator").GetString()!
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
        properties.Fields.Add("xenoarchaeologyData", Value.ForStruct(stringType));
        properties.Fields.Add("extractionProcedures", Value.ForStruct(stringArrayType));
        properties.Fields.Add("hostilityIndicator", Value.ForStruct(stringType));

        var responseFormat = new Struct();
        responseFormat.Fields.Add("type", Value.ForString("object"));
        responseFormat.Fields.Add("properties", Value.ForStruct(properties));
        responseFormat.Fields.Add("required", Value.ForList(
            Value.ForString("analysis"),
            Value.ForString("xenoarchaeologyData"),
            Value.ForString("extractionProcedures"),
            Value.ForString("hostilityIndicator")));

        return responseFormat;
    }
}
