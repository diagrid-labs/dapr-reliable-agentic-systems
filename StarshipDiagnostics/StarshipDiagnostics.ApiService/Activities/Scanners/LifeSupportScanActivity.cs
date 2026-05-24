using Dapr.AI.Conversation;
using Dapr.AI.Conversation.ConversationRoles;
using Dapr.AI.Conversation.Extensions;
using Dapr.Workflow;
using StarshipDiagnostics.Models;
using System.Text.Json;

namespace StarshipDiagnostics.Activities.Scanners;

public class LifeSupportScanActivity : WorkflowActivity<Starship, ScanResult>
{
    private readonly DaprConversationClient _conversationClient;

    public LifeSupportScanActivity(DaprConversationClient conversationClient)
    {
        _conversationClient = conversationClient;
    }

    public override async Task<ScanResult> RunAsync(
        WorkflowActivityContext context,
        Starship input)
    {
        var conversationOptions = new ConversationOptions("conversation")
        {
            Temperature = 0.5
        };

        var response = await _conversationClient.ConverseAsync(
            [
                new ConversationInput(new List<IConversationMessage>
                {
                    new SystemMessage
                    {
                        Content = [
                            new MessageContent(@"You are a life support systems expert. Analyze:
                            - Oxygen generation and recycling
                            - Water purification systems
                            - Artificial gravity generators
                            - Temperature and humidity control
                            - Air quality and contamination levels

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
                              ""healthPercentage"": 94,
                              ""issues"": [""Oxygen recycler efficiency slightly degraded in section B""],
                              ""recommendations"": [""Replace carbon dioxide scrubber filters during next scheduled maintenance""],
                              ""detailedAnalysis"": ""Life support systems are operating within normal parameters. All critical subsystems are functional with minor efficiency degradation in non-critical components...""
                            }")
                        ]
                    },
                    new UserMessage
                    {
                        Name = "LifeSupportScanner",
                        Content = [
                            new MessageContent($"Ship: {input.Name}, Years: {input.YearsInService}\n\nScan life support:")
                        ]
                    }
                })
            ],
            conversationOptions);

        return ParseScanResult("Life Support", response.Outputs.First().Choices.First().Message.Content);
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
