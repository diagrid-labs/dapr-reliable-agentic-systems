using Dapr.AI.Conversation;
using Dapr.AI.Conversation.Extensions;
using Dapr.AI.Conversation.ConversationRoles;
using Dapr.Workflow;
using System.Text.Json;

public class TranslateActivity : WorkflowActivity<TranslateInput, Translation>
{
    private readonly DaprConversationClient _conversationClient;
    
    public TranslateActivity(DaprConversationClient conversationClient)
    {
        _conversationClient = conversationClient;
    }
    
    public override async Task<Translation> RunAsync(
        WorkflowActivityContext context, 
        TranslateInput input)
    {
        var systemPrompt = @"You are an expert xenolinguist specializing in alien language 
translation for first contact scenarios. Your translations must:
- Be accurate to the original meaning
- Preserve cultural nuances and context
- Use appropriate idiomatic English expressions
- Maintain the tone and formality level
- Note any untranslatable concepts

Provide both the translation and your reasoning for key choices.

Respond **only** with valid JSON.
Do not include explanations, comments, or text outside the JSON object.
Ensure the JSON is syntactically correct and can be parsed without errors.
Use double quotes around all keys and string values.
Use opening and closing curly braces.

JSON structure that describes the fields:
{
  ""translation"": ""<translated text in English>"",
  ""reasoning"": ""<explanation of key translation choices>""
}

Example:
{
  ""translation"": ""Live long and prosper. Infinite diversity in infinite combinations. We treasure our bond."",
  ""reasoning"": ""Translated formal Vulcan greeting with emphasis on longevity and prosperity. IDIC philosophy referenced using 'infinite diversity' phrase. 'Taluhk' rendered as 'treasure' to convey emotional depth while maintaining diplomatic tone.""
}";

        var userPrompt = $@"Translate this {input.Text.AlienSpecies} text to English:

Original Text: {input.Text.OriginalText}
Context: {input.Text.Context}
Cultural Notes: {input.Text.CulturalNotes}

Known Vocabulary:
{string.Join("\n", input.Text.KnownVocabulary.Select(kv => $"- {kv.Key}: {kv.Value}"))}

Provide your translation and explain your reasoning for important translation choices.";

        var options = new ConversationOptions("conversation")
        {
            Temperature = 0.75
        };
        
        var response = await _conversationClient.ConverseAsync(
            [
                new ConversationInput(new List<IConversationMessage>
                {
                    new SystemMessage
                    {
                        Content = [new MessageContent(systemPrompt)]
                    },
                    new UserMessage
                    {
                        Name = "Xenolinguist",
                        Content = [new MessageContent(userPrompt)]
                    }
                })
            ],
            options);
        
        var json = JsonSerializer.Deserialize<JsonElement>(
            response.Outputs.First().Choices.First().Message.Content);
        
        return new Translation(
            input.Iteration,
            json.GetProperty("translation").GetString()!,
            json.GetProperty("reasoning").GetString()!,
            DateTime.UtcNow
        );
    }
}
