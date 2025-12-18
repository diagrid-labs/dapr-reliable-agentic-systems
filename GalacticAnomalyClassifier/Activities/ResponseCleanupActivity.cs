using Dapr.AI.Conversation;
using Dapr.AI.Conversation.ConversationRoles;
using Dapr.Workflow;
using System.Text.Json;

namespace GalacticAnomalyClassifier.Activities;

public class ResponseCleanupActivity : WorkflowActivity<string, JsonElement>
{
    private readonly DaprConversationClient _conversationClient;
    
    public ResponseCleanupActivity(DaprConversationClient conversationClient)
    {
        _conversationClient = conversationClient;
    }
    
    public override async Task<JsonElement> RunAsync(
        WorkflowActivityContext context, 
        string input)
    {
        var conversationOptions = new ConversationOptions("conversation")
        {
            Temperature = 0.9
        };
        
        var response = await _conversationClient.ConverseAsync(
            [
                new ConversationInput(new List<IConversationMessage>
                {
                    new SystemMessage
                    {
                        Content = [
                            new MessageContent(@"You are an AI response formatter and cleaner.
                            You convert incoming markdown json code blocks to ensure they strictly adhere to JSON format.
                            You remove the markdown code block formatting so only the JSON remains.
                            
                            INPUT:
                            
                            ```json
                            {
                              ""field1"" : ""value1"",
                              ""field2"" : ""value2""
                            }
                            ```

                            EXPECTED OUTPUT:
                            
                            {
                              ""<field1>"" : ""<value1>"",
                              ""<field2>"" : ""<value2>""
                            }
                            ")
                        ]
                    },
                    new UserMessage
                    {
                        Name = "InputToClean",
                        Content = [
                            new MessageContent($"Convert this input:\n{input}")
                        ]
                    }
                })
            ],
            conversationOptions);
        
        Console.WriteLine($"Clean up Response: {response.Outputs.First().Choices.First().Message.Content}");

        var json = JsonSerializer.Deserialize<JsonElement>(
            response.Outputs.First().Choices.First().Message.Content);
        
        return json;
    }
}
