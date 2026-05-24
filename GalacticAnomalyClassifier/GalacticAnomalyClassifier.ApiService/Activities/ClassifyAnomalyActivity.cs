using Dapr.AI.Conversation;
using Dapr.AI.Conversation.ConversationRoles;
using Dapr.AI.Conversation.Extensions;
using Dapr.Workflow;
using GalacticAnomalyClassifier.Models;
using Google.Protobuf.WellKnownTypes;
using System.Text.Json;

namespace GalacticAnomalyClassifier.Activities;

public class ClassifyAnomalyActivity : WorkflowActivity<SpaceAnomaly, AnomalyClassification>
{
    private readonly DaprConversationClient _conversationClient;
    
    public ClassifyAnomalyActivity(DaprConversationClient conversationClient)
    {
        _conversationClient = conversationClient;
    }
    
    public override async Task<AnomalyClassification> RunAsync(
        WorkflowActivityContext context, 
        SpaceAnomaly input)
    {
        var sensorSummary = $@"
Sensor Data: {input.SensorData}
Coordinates: {input.Coordinates}
Measurements: {string.Join(", ", input.Measurements.Select(m => $"{m.Key}={m.Value}"))}
";

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
                            new MessageContent(@"You are an advanced anomaly classification AI for a deep space 
                            science station. Classify space anomalies into one of these categories:
                            
                            1. TEMPORAL RIFT - Time distortions, chronological anomalies, causality violations
                            2. DARK MATTER CLUSTER - Unusual gravitational fields, invisible mass concentrations
                            3. ALIEN ARTIFACT - Manufactured objects, ancient technology, non-natural structures
                            4. STELLAR PHENOMENON - Star-related events, supernovae, neutron star activity
                            5. DIMENSIONAL TEAR - Reality breaches, multiverse intrusions, spatial ruptures

                            JSON structure that describes the fields:
                            {
                              ""type"": ""<category name>"",
                              ""confidence"": <0.0 to 1.0>,
                              ""reasoning"": ""<brief explanation>""
                            }
                            ")
                        ]
                    },
                    new UserMessage
                    {
                        Name = "AnomalyClassifier",
                        Content = [
                            new MessageContent($"Classify this anomaly:\n{sensorSummary}")
                        ]
                    }
                })
            ],
            conversationOptions);

        var json = JsonSerializer.Deserialize<JsonElement>(
            response.Outputs.First().Choices.First().Message.Content);
        
        return new AnomalyClassification(
            json.GetProperty("type").GetString()!,
            json.GetProperty("confidence").GetDouble(),
            json.GetProperty("reasoning").GetString()!
        );
    }

    private static Struct GetResponseFormat()
    {
        var stringType = new Struct();
        stringType.Fields.Add("type", Value.ForString("string"));

        var numberType = new Struct();
        numberType.Fields.Add("type", Value.ForString("number"));

        var properties = new Struct();
        properties.Fields.Add("type", Value.ForStruct(stringType));
        properties.Fields.Add("confidence", Value.ForStruct(numberType));
        properties.Fields.Add("reasoning", Value.ForStruct(stringType));

        var responseFormat = new Struct();
        responseFormat.Fields.Add("type", Value.ForString("object"));
        responseFormat.Fields.Add("properties", Value.ForStruct(properties));
        responseFormat.Fields.Add("required", Value.ForList(
            Value.ForString("type"),
            Value.ForString("confidence"),
            Value.ForString("reasoning")));

        return responseFormat;
    }
}
