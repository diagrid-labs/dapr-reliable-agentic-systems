using Dapr.AI.Conversation;
using Dapr.AI.Conversation.ConversationRoles;
using Dapr.AI.Conversation.Extensions;
using Dapr.Workflow;
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
            Temperature = 0.7
        };
        
        var response = await _conversationClient.ConverseAsync(
            [
                new ConversationInput(new List<IConversationMessage>
                {
                    new SystemMessage
                    {
                        Content = [new MessageContent(@"Generate a realistic space debris field scan result. 
                    Include 3-8 debris objects with varied types, masses, and threat levels.
                    
                    Respond **only** with valid JSON.
                    Do not include explanations, comments, or text outside the JSON object.
                    Ensure the JSON is syntactically correct and can be parsed without errors.
                    Use double quotes around all keys and string values.
                    Use opening and closing curly braces.
                    
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
                    }

                    Example:
                    {
                      ""debris"": [
                        {
                          ""id"": ""DEB-001"",
                          ""mass"": 450.5,
                          ""type"": ""Satellite"",
                          ""position"": [1200.0, -450.0, 350.0],
                          ""velocity"": [7.5, -0.3, 0.1],
                          ""threatLevel"": ""High"",
                          ""isFragmented"": false
                        },
                        {
                          ""id"": ""DEB-002"",
                          ""mass"": 85.2,
                          ""type"": ""Fragment"",
                          ""position"": [1180.0, -440.0, 360.0],
                          ""velocity"": [7.6, -0.5, 0.2],
                          ""threatLevel"": ""Medium"",
                          ""isFragmented"": true
                        }
                      ],
                      ""totalMass"": 535.7,
                      ""riskLevel"": ""High""
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
}
