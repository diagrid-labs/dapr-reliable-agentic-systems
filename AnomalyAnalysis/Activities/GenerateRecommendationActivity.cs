using Dapr.Workflow;
using Dapr.AI.Conversation;
using Dapr.AI.Conversation.ConversationRoles;

namespace AnomalyAnalysis.Activities;

public class GenerateRecommendationActivity : WorkflowActivity<object, string>
{
    private readonly DaprConversationClient _conversationClient;
    
    public GenerateRecommendationActivity(DaprConversationClient conversationClient)
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
        string riskLevel = data.RiskLevel;
        
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
                            new MessageContent(@"You are Data's tactical recommendation system for Captain Picard. 
                            Generate clear, actionable recommendations for spatial anomalies:
                            - INVESTIGATE: Deploy shuttlecraft with sensor package, maintain safe distance
                            - STUDY_FROM_DISTANCE: Use long-range sensors, collect data without approach
                            - AVOID: Set course around anomaly, log coordinates for Starfleet Science
                            - EMERGENCY_EVASION: Immediate warp speed departure, shields to maximum
                            - REPORT_TO_STARFLEET: Notify Starfleet Command and Science Division
                            
                            Include specific tactical details for implementation.")
                        ]
                    },
                    new UserMessage
                    {
                        Name = "DataTactical",
                        Content = [
                            new MessageContent($"Generate recommendation for {anomalyType} (Risk: {riskLevel}):\n{analysis}")
                        ]
                    }
                })
            ],
            conversationOptions);
        
        return response.Outputs.First().Choices.First().Message.Content;
    }
}
