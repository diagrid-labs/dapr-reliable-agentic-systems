using Dapr.AI.Conversation;
using Dapr.AI.Conversation.ConversationRoles;
using Dapr.AI.Conversation.Extensions;
using Dapr.Workflow;
using StarshipDiagnostics.Models;
using System.Text.Json;

namespace StarshipDiagnostics.Activities.Scanners;

public class NavigationScanActivity : WorkflowActivity<Starship, ScanResult>
{
    private readonly DaprConversationClient _conversationClient;

    public NavigationScanActivity(DaprConversationClient conversationClient)
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
                            new MessageContent(@"You are a navigation systems specialist AI. Analyze for:
                            - Warp drive calibration accuracy
                            - Navigational deflector functionality
                            - Sensor array precision
                            - Stellar cartography database integrity
                            - FTL jump calculation accuracy

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
                              ""status"": ""OK"",
                              ""healthPercentage"": 92,
                              ""issues"": [""Minor drift in long-range sensor calibration""],
                              ""recommendations"": [""Recalibrate sensor array during next maintenance window""],
                              ""detailedAnalysis"": ""Navigation systems are operating within acceptable parameters. Warp drive calibration is precise and stellar cartography database is current...""
                            }")
                        ]
                    },
                    new UserMessage
                    {
                        Name = "NavigationScanner",
                        Content = [
                            new MessageContent($@"Ship: {input.Name}
Years in service: {input.YearsInService}
Telemetry: {telemetryData}

Scan navigation systems:")
                        ]
                    }
                })
            ],
            conversationOptions);

        return ParseScanResult("Navigation Systems", response.Outputs.First().Choices.First().Message.Content);
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
