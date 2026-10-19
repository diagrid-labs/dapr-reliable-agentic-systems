using Dapr.Workflow;
using AnomalyAnalysis.Models;
using Dapr.AI.Conversation;
using Dapr.AI.Conversation.ConversationRoles;

namespace AnomalyAnalysis.Activities;

public class ScientificAnalysisActivity : WorkflowActivity<ScientificAnalysisInput, Metered<string>>
{
    private readonly DaprConversationClient _conversationClient;
    
    public ScientificAnalysisActivity(DaprConversationClient conversationClient)
    {
        _conversationClient = conversationClient;
    }
    
    public override async Task<Metered<string>> RunAsync(
        WorkflowActivityContext context, 
        ScientificAnalysisInput input)
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
                            new MessageContent(@"You are Data's scientific analysis system. Provide detailed 
                            analysis of spatial anomalies including: formation theories, energy 
                            signatures, spatial dimensions, stability factors, scientific significance, 
                            and potential for research. Use appropriate astrophysics terminology.".FormatToMarkDown(wordLength: 75))
                        ]
                    },
                    new UserMessage
                    {
                        Name = "DataScience",
                        Content = [
                            new MessageContent($"Analyze {input.AnomalyType}: {input.ProcessedData}")
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

public record ScientificAnalysisInput(string ProcessedData, string AnomalyType);
