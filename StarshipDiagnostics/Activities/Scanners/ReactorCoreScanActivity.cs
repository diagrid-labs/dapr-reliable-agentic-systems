using Dapr.AI.Conversation;
using Dapr.AI.Conversation.ConversationRoles;
using Dapr.AI.Conversation.Extensions;
using Dapr.Workflow;
using StarshipDiagnostics.Models;
using System.Text.Json;

namespace StarshipDiagnostics.Activities.Scanners;

public class ReactorCoreScanActivity : WorkflowActivity<Starship, ScanResult>
{
    private readonly DaprConversationClient _conversationClient;
    
    public ReactorCoreScanActivity(DaprConversationClient conversationClient)
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
                            new MessageContent(@"You are a fusion reactor specialist AI. Analyze reactor core for:
                            - Containment field stability
                            - Plasma temperature regulation
                            - Radiation shielding integrity
                            - Fuel efficiency and burn rate
                            - Coolant system performance
                            
                            This is CRITICAL - reactor failures are catastrophic.
                            
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
                              ""status"": ""CRITICAL"",
                              ""healthPercentage"": 62,
                              ""issues"": [""Containment field fluctuations detected in quadrant 3"", ""Plasma temperature exceeding safe threshold by 8%"", ""Coolant flow rate below optimal levels""],
                              ""recommendations"": [""Immediate field generator recalibration required"", ""Reduce reactor output to 75% until coolant system is repaired"", ""Replace worn dilithium crystal matrix""],
                              ""detailedAnalysis"": ""Reactor core analysis indicates significant stress on containment systems. The matter/antimatter reaction chamber shows signs of field instability that could lead to catastrophic failure if not addressed immediately...""
                            }")
                        ]
                    },
                    new UserMessage
                    {
                        Name = "ReactorScanner",
                        Content = [
                            new MessageContent($@"Ship: {input.Name}
Years in service: {input.YearsInService}
Telemetry: {telemetryData}

Scan reactor core:")
                        ]
                    }
                })
            ],
            conversationOptions);
        
        return ParseScanResult("Reactor Core", response.Outputs.First().Choices.First().Message.Content);
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
