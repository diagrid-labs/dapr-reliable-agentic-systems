using Dapr.Workflow;
using Dapr.AI.Conversation;
using Dapr.AI.Conversation.Extensions;

namespace AnomalyAnalysis.Activities;

public class ClassifyAnomalyActivity : WorkflowActivity<string, string>
{
    private readonly DaprConversationClient _conversationClient;
    
    public ClassifyAnomalyActivity(DaprConversationClient conversationClient)
    {
        _conversationClient = conversationClient;
    }
    
    public override async Task<string> RunAsync(
        WorkflowActivityContext context, 
        string processedData)
    {
        var conversationOptions = new ConversationOptions("conversation")
        {
            Temperature = 0.3,
            Model = "gpt-4o"
        };
        
        var response = await _conversationClient.ConverseAsync(
            [
                new ConversationInput(new List<IConversationMessage>
                {
                    new SystemMessage
                    {
                        Content = [
                            new MessageContent(@"You are Data's scientific classification system. 
                            Classify spatial anomalies based on sensor data. Categories include: 
                            WORMHOLE, SUBSPACE_RIFT, QUANTUM_SINGULARITY, TEMPORAL_DISTORTION, 
                            DARK_MATTER_CLOUD, STELLAR_NURSERY, NEBULA, GRAVIMETRIC_DISTORTION. 
                            Return only the classification type.")
                        ]
                    },
                    new UserMessage
                    {
                        Name = "DataClassification",
                        Content = [
                            new MessageContent($"Classify this anomaly: {processedData}")
                        ]
                    }
                })
            ],
            conversationOptions);
        
        return response.Outputs.First().Choices.First().Message.Content.Trim();
    }
}
