using Dapr.AI.Conversation;
using Dapr.AI.Conversation.ConversationRoles;
using Dapr.AI.Conversation.Extensions;
using Dapr.Workflow;
using StarshipDiagnostics.Models;
using System.Text.Json;

namespace StarshipDiagnostics.Activities.Scanners;

public class HullIntegrityScanActivity : WorkflowActivity<Starship, ScanResult>
{
    private readonly DaprConversationClient _conversationClient;

    public HullIntegrityScanActivity(DaprConversationClient conversationClient)
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
            Temperature = 0.7,
            ResponseFormat = ScanResultSchema.Get()
        };

        var response = await _conversationClient.ConverseAsync(
            [
                new ConversationInput(new List<IConversationMessage>
                {
                    new SystemMessage
                    {
                        Content = [
                            new MessageContent(@"You are a hull integrity scanner AI. Analyze starship hull condition for:
                            - Micrometeorite impacts
                            - Stress fractures from FTL travel
                            - Corrosion from space radiation
                            - Structural weak points

                            Respond with the following JSON structure:
                            {
                              ""status"": ""<OK|WARNING|CRITICAL>"",
                              ""healthPercentage"": <0-100>,
                              ""issues"": [""<issue1>"", ""<issue2>""],
                              ""recommendations"": [""<rec1>"", ""<rec2>""],
                              ""detailedAnalysis"": ""<full analysis text>""
                            }

                            Keep `detailedAnalysis` <=80 words. `issues` and `recommendations` <=5 items each, each <=15 words. Return only the JSON object with no surrounding prose.")
                        ]
                    },
                    new UserMessage
                    {
                        Name = "HullScanner",
                        Content = [
                            new MessageContent($@"Ship: {input.Name} ({input.Class})
Years in service: {input.YearsInService}
Last maintenance: {input.LastMaintenance}
Telemetry: {telemetryData}

Scan hull integrity:")
                        ]
                    }
                })
            ],
            conversationOptions);

        return ParseScanResult("Hull Integrity", response.Outputs.First().Choices.First().Message.Content);
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
