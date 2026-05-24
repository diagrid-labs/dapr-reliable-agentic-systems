using Dapr.AI.Conversation;
using Dapr.AI.Conversation.ConversationRoles;
using Dapr.AI.Conversation.Extensions;
using Dapr.Workflow;
using StarshipDiagnostics.Models;

namespace StarshipDiagnostics.Activities.Voters;

public class SeverityVoterActivity : WorkflowActivity<ScanResult, KeyValuePair<string, string>>
{
    private readonly DaprConversationClient _conversationClient;

    public SeverityVoterActivity(DaprConversationClient conversationClient)
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
                            new MessageContent(@"You are a SEVERITY-FOCUSED evaluator. Analyze the technical severity of the finding.

                            Respond **only** with one of these exact values (no quotes, no explanations):
                            - IMMEDIATE_GROUNDING
                            - URGENT_REPAIR
                            - SCHEDULED_MAINTENANCE

                            Classification criteria:
                            - IMMEDIATE_GROUNDING: High likelihood of catastrophic failure, imminent danger
                            - URGENT_REPAIR: Time to failure measured in hours/days, cascading failure risk
                            - SCHEDULED_MAINTENANCE: Gradual degradation, failure unlikely in short term

                            Consider:
                            - Likelihood of catastrophic failure
                            - Time to failure if not addressed
                            - Potential for cascading failures

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
                        Name = "SeverityEvaluator",
                        Content = [
                            new MessageContent($@"Subsystem: {input.SubsystemName}
Analysis: {input.DetailedAnalysis}

Vote on severity classification:")
                        ]
                    }
                })
            ],
            conversationOptions);

        return new KeyValuePair<string, string>(
            "SeverityVoter",
            response.Outputs.First().Choices.First().Message.Content.Trim());
    }
}
