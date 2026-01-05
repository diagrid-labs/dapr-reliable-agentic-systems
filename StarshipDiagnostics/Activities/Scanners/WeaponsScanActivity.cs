using Dapr.AI.Conversation;
using Dapr.AI.Conversation.ConversationRoles;
using Dapr.AI.Conversation.Extensions;
using Dapr.Workflow;
using StarshipDiagnostics.Models;
using System.Text.Json;

namespace StarshipDiagnostics.Activities.Scanners;

public class WeaponsScanActivity : WorkflowActivity<Starship, ScanResult>
{
    private readonly DaprConversationClient _conversationClient;
    
    public WeaponsScanActivity(DaprConversationClient conversationClient)
    {
        _conversationClient = conversationClient;
    }
    
    public override async Task<ScanResult> RunAsync(
        WorkflowActivityContext context, 
        Starship input)
    {
        var telemetryData = JsonSerializer.Serialize(input.Telemetry);
        
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
                            new MessageContent(@"You are a tactical systems specialist AI. Analyze for:
                            - Phaser array power and alignment
                            - Photon torpedo launcher status
                            - Shield generator capacity and coverage
                            - Targeting computer accuracy
                            - Defensive countermeasure systems
                            
                            Respond **only** with valid JSON.
                            Do not include explanations, comments, or text outside the JSON object.
                            Ensure the JSON is syntactically correct and can be parsed without errors.
                            Use double quotes around all keys and string values.
                            Use opening and closing curly braces.

                            JSON structure that describes the fields:
                            {
                              ""status"": ""<OK|WARNING|CRITICAL>"",
                              ""healthPercentage"": <0-100>,
                              ""issues"": [""<issue1>"", ""<issue2>""],
                              ""recommendations"": [""<rec1>"", ""<rec2>""],
                              ""detailedAnalysis"": ""<full analysis text>""
                            }

                            Example:
                            {
                              ""status"": ""WARNING"",
                              ""healthPercentage"": 85,
                              ""issues"": [""Phaser array power coupling degraded by 12%"", ""Shield emitter 7 showing reduced output""],
                              ""recommendations"": [""Replace power coupling in phaser array 3"", ""Repair or replace shield emitter 7""],
                              ""detailedAnalysis"": ""Tactical systems are generally operational but showing age-related wear. Primary weapons systems functional but at reduced efficiency...""
                            }")
                        ]
                    },
                    new UserMessage
                    {
                        Name = "WeaponsScanner",
                        Content = [
                            new MessageContent($@"Ship: {input.Name}
Years in service: {input.YearsInService}
Telemetry: {telemetryData}

Scan tactical systems:")
                        ]
                    }
                })
            ],
            conversationOptions);
        
        return ParseScanResult("Tactical Systems", response.Outputs.First().Choices.First().Message.Content);
    }
    
    private ScanResult ParseScanResult(string subsystem, string jsonContent)
    {
        var json = JsonSerializer.Deserialize<JsonElement>(jsonContent);
        return new ScanResult(
            subsystem,
            json.GetProperty("status").GetString(),
            json.GetProperty("healthPercentage").GetDouble(),
            JsonSerializer.Deserialize<List<string>>(
                json.GetProperty("issues").GetRawText()),
            JsonSerializer.Deserialize<List<string>>(
                json.GetProperty("recommendations").GetRawText()),
            json.GetProperty("detailedAnalysis").GetString()
        );
    }
}
