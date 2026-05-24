using Dapr.AI.Conversation;
using Dapr.AI.Conversation.ConversationRoles;
using Dapr.AI.Conversation.Extensions;
using Dapr.Workflow;
using StarshipDiagnostics.Models;

namespace StarshipDiagnostics.Activities.Voters;

public class SafetyVoterActivity : WorkflowActivity<ScanResult, KeyValuePair<string, string>>
{
    private readonly DaprConversationClient _conversationClient;

    public SafetyVoterActivity(DaprConversationClient conversationClient)
    {
        _conversationClient = conversationClient;
    }

    public override async Task<KeyValuePair<string, string>> RunAsync(
        WorkflowActivityContext context,
        ScanResult input)
    {
        var conversationOptions = new ConversationOptions("conversation")
        {
            Temperature = 0.3
        };

        var response = await _conversationClient.ConverseAsync(
            [
                new ConversationInput(new List<IConversationMessage>
                {
                    new SystemMessage
                    {
                        Content = [
                            new MessageContent(@"You are a SAFETY-FOCUSED evaluator. Analyze the finding from a crew safety perspective.

                            Respond **only** with one of these exact values (no quotes, no explanations):
                            - IMMEDIATE_GROUNDING
                            - URGENT_REPAIR
                            - SCHEDULED_MAINTENANCE

                            Classification criteria:
                            - IMMEDIATE_GROUNDING: Ship cannot safely operate, immediate crew danger
                            - URGENT_REPAIR: Must fix before next voyage, safety risk if ignored
                            - SCHEDULED_MAINTENANCE: Can wait for next scheduled service, minimal safety impact

                            Example responses:
                            IMMEDIATE_GROUNDING
                            or
                            URGENT_REPAIR
                            or
                            SCHEDULED_MAINTENANCE")
                        ]
                    },
                    new UserMessage
                    {
                        Name = "SafetyEvaluator",
                        Content = [
                            new MessageContent($@"Subsystem: {input.SubsystemName}
Status: {input.Status}
Health: {input.HealthPercentage}%
Issues: {string.Join(", ", input.Issues ?? new List<string>())}

Vote on safety classification:")
                        ]
                    }
                })
            ],
            conversationOptions);

        return new KeyValuePair<string, string>(
            "SafetyVoter",
            response.Outputs.First().Choices.First().Message.Content.Trim());
    }
}
