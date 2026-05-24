using Dapr.AI.Conversation;
using Dapr.AI.Conversation.ConversationRoles;
using Dapr.AI.Conversation.Extensions;
using Dapr.Workflow;
using StarshipDiagnostics.Models;

namespace StarshipDiagnostics.Activities.Voters;

public class RecommendationVoterActivity : WorkflowActivity<ScanResult, KeyValuePair<string, string>>
{
    private readonly DaprConversationClient _conversationClient;

    public RecommendationVoterActivity(DaprConversationClient conversationClient)
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
                            new MessageContent(@"You are a COST-BENEFIT evaluator. Balance repair urgency against operational needs and costs.

                            Respond **only** with one of these exact values (no quotes, no explanations):
                            - IMMEDIATE_GROUNDING
                            - URGENT_REPAIR
                            - SCHEDULED_MAINTENANCE

                            Classification criteria:
                            - IMMEDIATE_GROUNDING: No safe workarounds, repair costs exceed mission value if delayed
                            - URGENT_REPAIR: Temporary measures possible but expensive/risky, fix before next mission
                            - SCHEDULED_MAINTENANCE: Cost-effective to defer, minimal operational impact

                            Consider:
                            - Can the ship operate safely with temporary measures?
                            - Cost of immediate repair vs scheduled maintenance
                            - Mission criticality and operational urgency

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
                        Name = "CostBenefitEvaluator",
                        Content = [
                            new MessageContent($@"Subsystem: {input.SubsystemName}
Recommendations: {string.Join(", ", input.Recommendations ?? new List<string>())}

Vote on action classification:")
                        ]
                    }
                })
            ],
            conversationOptions);

        return new KeyValuePair<string, string>(
            "RecommendationVoter",
            response.Outputs.First().Choices.First().Message.Content.Trim());
    }
}
