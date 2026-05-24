using Dapr.AI.Conversation;
using Dapr.AI.Conversation.ConversationRoles;
using Dapr.AI.Conversation.Extensions;
using Dapr.Workflow;
using Google.Protobuf.WellKnownTypes;
using SpaceDebrisAgent.Models;
using System.Text.Json;

namespace SpaceDebrisAgent.Activities.Tools;

public class ScanDebrisFieldActivity : WorkflowActivity<Dictionary<string, object>, DebrisField>
{
    private readonly DaprConversationClient _conversationClient;
    
    public ScanDebrisFieldActivity(DaprConversationClient conversationClient)
    {
        _conversationClient = conversationClient;
    }
    
    public override async Task<DebrisField> RunAsync(
        WorkflowActivityContext context, 
        Dictionary<string, object> parameters)
    {
        // In real implementation, this would interface with actual sensors
        // For demo, we'll generate a simulated debris field
        
        var options = new ConversationOptions("conversation")
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
                        Content = [new MessageContent(@"Generate a realistic space debris field scan result.
                    Include 3-8 debris objects with varied types, masses, and threat levels.

                    JSON structure that describes the fields:
                    {
                      ""debris"": [
                        {
                          ""id"": ""<string: unique debris identifier>"",
                          ""mass"": <number: mass in kg>,
                          ""type"": ""<string: Satellite|RocketStage|Fragment>"",
                          ""position"": [<number: x km>, <number: y km>, <number: z km>],
                          ""velocity"": [<number: vx km/s>, <number: vy km/s>, <number: vz km/s>],
                          ""threatLevel"": ""<string: Low|Medium|High|Critical>"",
                          ""isFragmented"": <boolean: true or false>
                        }
                      ],
                      ""totalMass"": <number: sum of all debris mass>,
                      ""riskLevel"": ""<string: Low|Medium|High|Critical>""
                    }")]
                    },
                    new UserMessage
                    {
                        Name = "DebrisScanner",
                        Content = [new MessageContent("Scan debris field in LEO")]
                    }
                })
            ],
            options);
        
        return JsonSerializer.Deserialize<DebrisField>(
            response.Outputs.First().Choices.First().Message.Content)!;
    }

    private static Struct GetResponseFormat()
    {
        var stringType = new Struct();
        stringType.Fields.Add("type", Value.ForString("string"));

        var numberType = new Struct();
        numberType.Fields.Add("type", Value.ForString("number"));

        var booleanType = new Struct();
        booleanType.Fields.Add("type", Value.ForString("boolean"));

        var numberArrayType = new Struct();
        numberArrayType.Fields.Add("type", Value.ForString("array"));
        numberArrayType.Fields.Add("items", Value.ForStruct(numberType));

        var debrisProps = new Struct();
        debrisProps.Fields.Add("id", Value.ForStruct(stringType));
        debrisProps.Fields.Add("mass", Value.ForStruct(numberType));
        debrisProps.Fields.Add("type", Value.ForStruct(stringType));
        debrisProps.Fields.Add("position", Value.ForStruct(numberArrayType));
        debrisProps.Fields.Add("velocity", Value.ForStruct(numberArrayType));
        debrisProps.Fields.Add("threatLevel", Value.ForStruct(stringType));
        debrisProps.Fields.Add("isFragmented", Value.ForStruct(booleanType));

        var debrisItemType = new Struct();
        debrisItemType.Fields.Add("type", Value.ForString("object"));
        debrisItemType.Fields.Add("properties", Value.ForStruct(debrisProps));
        debrisItemType.Fields.Add("required", Value.ForList(
            Value.ForString("id"),
            Value.ForString("mass"),
            Value.ForString("type"),
            Value.ForString("position"),
            Value.ForString("velocity"),
            Value.ForString("threatLevel"),
            Value.ForString("isFragmented")));

        var debrisArrayType = new Struct();
        debrisArrayType.Fields.Add("type", Value.ForString("array"));
        debrisArrayType.Fields.Add("items", Value.ForStruct(debrisItemType));

        var properties = new Struct();
        properties.Fields.Add("debris", Value.ForStruct(debrisArrayType));
        properties.Fields.Add("totalMass", Value.ForStruct(numberType));
        properties.Fields.Add("riskLevel", Value.ForStruct(stringType));

        var responseFormat = new Struct();
        responseFormat.Fields.Add("type", Value.ForString("object"));
        responseFormat.Fields.Add("properties", Value.ForStruct(properties));
        responseFormat.Fields.Add("required", Value.ForList(
            Value.ForString("debris"),
            Value.ForString("totalMass"),
            Value.ForString("riskLevel")));

        return responseFormat;
    }
}
