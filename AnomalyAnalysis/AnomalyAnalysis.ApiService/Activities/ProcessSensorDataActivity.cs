using Dapr.Workflow;
using AnomalyAnalysis.Models;
using Dapr.AI.Conversation;
using Dapr.AI.Conversation.ConversationRoles;

namespace AnomalyAnalysis.Activities;

public class ProcessSensorDataActivity : WorkflowActivity<string, Metered<string>>
{
    private readonly DaprConversationClient _conversationClient;
    
    public ProcessSensorDataActivity(DaprConversationClient conversationClient)
    {
        _conversationClient = conversationClient;
    }
    
    public override async Task<Metered<string>> RunAsync(
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
                            (wavelength, frequency, intensity, spatial coordinates).".FormatToMarkDown(wordLength: 25))
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
        
        return new Metered<string>(
            response.Outputs.First().Choices.First().Message.Content,
            response.ToUsage());
    }
}
