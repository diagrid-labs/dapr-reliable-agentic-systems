using Dapr.Workflow;
using Dapr.AI.Conversation;
using Dapr.AI.Conversation.ConversationRoles;

namespace AnomalyAnalysis.Activities;

public class ScientificAnalysisActivity : WorkflowActivity<object, string>
{
    private readonly DaprConversationClient _conversationClient;
    
    public ScientificAnalysisActivity(DaprConversationClient conversationClient)
    {
        _conversationClient = conversationClient;
    }
    
    public override async Task<string> RunAsync(
        WorkflowActivityContext context, 
        object input)
    {
        var data = (dynamic)input;
        string processedData = data.ProcessedData;
        string anomalyType = data.AnomalyType;
        
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
                            new MessageContent(@"You are Data's scientific analysis system. Provide detailed 
                            analysis of spatial anomalies including: formation theories, energy 
                            signatures, spatial dimensions, stability factors, scientific significance, 
                            and potential for research. Use appropriate astrophysics terminology.")
                        ]
                    },
                    new UserMessage
                    {
                        Name = "DataScience",
                        Content = [
                            new MessageContent($"Analyze {anomalyType}: {processedData}")
                        ]
                    }
                })
            ],
            conversationOptions);
        
        return response.Outputs.First().Choices.First().Message.Content;
    }
}
