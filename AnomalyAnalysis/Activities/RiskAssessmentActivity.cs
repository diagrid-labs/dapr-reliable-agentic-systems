using Dapr.Workflow;
using Dapr.AI.Conversation;
using Dapr.AI.Conversation.ConversationRoles;

namespace AnomalyAnalysis.Activities;

public class RiskAssessmentActivity : WorkflowActivity<object, string>
{
    private readonly DaprConversationClient _conversationClient;
    
    public RiskAssessmentActivity(DaprConversationClient conversationClient)
    {
        _conversationClient = conversationClient;
    }
    
    public override async Task<string> RunAsync(
        WorkflowActivityContext context, 
        object input)
    {
        var data = (dynamic)input;
        string anomalyType = data.AnomalyType;
        string analysis = data.Analysis;
        
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
                            new MessageContent(@"You are Geordi La Forge and Worf's combined risk assessment system. 
                            Evaluate spatial anomalies for danger to the Enterprise: 
                            - LOW: No immediate danger, safe to approach
                            - MODERATE: Potential risks, caution advised
                            - HIGH: Significant danger, shields and defensive posture required
                            - CRITICAL: Immediate threat to ship integrity or crew safety
                            
                            Consider: radiation levels, gravitational stress on hull, subspace interference, 
                            temporal effects. Return only the risk level.")
                        ]
                    },
                    new UserMessage
                    {
                        Name = "GeordiAndWorf",
                        Content = [
                            new MessageContent($"Assess risk for {anomalyType}:\n{analysis}")
                        ]
                    }
                })
            ],
            conversationOptions);
        
        return response.Outputs.First().Choices.First().Message.Content.Trim();
    }
}
