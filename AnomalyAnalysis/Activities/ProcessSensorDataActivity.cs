using Dapr.Workflow;
using Dapr.AI.Conversation;
using Dapr.AI.Conversation.ConversationRoles;

namespace AnomalyAnalysis.Activities;

public class ProcessSensorDataActivity : WorkflowActivity<string, string>
{
    private readonly DaprConversationClient _conversationClient;
    
    public ProcessSensorDataActivity(DaprConversationClient conversationClient)
    {
        _conversationClient = conversationClient;
    }
    
    public override async Task<string> RunAsync(
        WorkflowActivityContext context, 
        string rawData)
    {
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
                            new MessageContent(@"You are Lt. Commander Data's sensor analysis subroutine. 
                            Process raw sensor data from the Enterprise's long-range scanners. 
                            Convert electromagnetic readings, subspace distortions, and quantum 
                            fluctuations into structured scientific data with key measurements 
                            (wavelength, frequency, intensity, spatial coordinates).")
                        ]
                    },
                    new UserMessage
                    {
                        Name = "DataAnalysis",
                        Content = [
                            new MessageContent($"Process sensor data: {rawData}")
                        ]
                    }
                })
            ],
            conversationOptions);
        
        return response.Outputs.First().Choices.First().Message.Content;
    }
}
