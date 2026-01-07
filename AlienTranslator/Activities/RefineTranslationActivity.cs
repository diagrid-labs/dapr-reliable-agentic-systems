using Dapr.AI.Conversation;
using Dapr.AI.Conversation.Extensions;
using Dapr.AI.Conversation.ConversationRoles;
using Dapr.Workflow;
using System.Text.Json;

public class RefineTranslationActivity : WorkflowActivity<RefineInput, Translation>
{
    private readonly DaprConversationClient _conversationClient;
    
    public RefineTranslationActivity(DaprConversationClient conversationClient)
    {
        _conversationClient = conversationClient;
    }
    
    public override async Task<Translation> RunAsync(
        WorkflowActivityContext context, 
        RefineInput input)
    {
        Console.WriteLine($"LOG RefineTranslationActivity {input}");
        
        var systemPrompt = @"You are an expert xenolinguist refining a translation based on 
detailed editorial feedback. Your goal is to address the specific weaknesses identified 
while maintaining the strengths of the current translation.

Respond **only** with valid JSON.
Do not include explanations, comments, or text outside the JSON object.
Ensure the JSON is syntactically correct and can be parsed without errors.
Use double quotes around all keys and string values.
Use opening and closing curly braces.

JSON structure that describes the fields:
{
  ""translation"": ""<improved translated text>"",
  ""reasoning"": ""<explanation of changes made to address feedback>""
}

Example:
{
  ""translation"": ""May you live long and prosper. We embrace infinite diversity in infinite combinations. Our alliance is precious to us."",
  ""reasoning"": ""Improved opening with 'May you' for more diplomatic tone. Changed 'infinite diversity' to 'We embrace infinite diversity' to show active participation in IDIC philosophy. Replaced 'treasure our bond' with 'alliance is precious' for clearer diplomatic context while maintaining emotional nuance.""
}";

        var userPrompt = $@"Refine this translation based on evaluator feedback:

Original {input.Text.AlienSpecies} Text: {input.Text.OriginalText}
Context: {input.Text.Context}

Current Translation (Iteration {input.Current.IterationNumber}):
{input.Current.TranslatedText}

Evaluation Scores:
- Accuracy: {input.Feedback.AccuracyScore}/10
- Cultural Nuance: {input.Feedback.CulturalNuanceScore}/10
- Idiomatic Quality: {input.Feedback.IdiomaticScore}/10
- Overall: {input.Feedback.OverallQuality}/10

Strengths:
{string.Join("\n", input.Feedback.Strengths.Select(s => $"- {s}"))}

Weaknesses to Address:
{string.Join("\n", input.Feedback.Weaknesses.Select(w => $"- {w}"))}

Detailed Feedback:
{input.Feedback.DetailedFeedback}

Provide an improved translation that addresses the weaknesses while preserving the strengths.
Return JSON with: translation (string), reasoning (string explaining changes made).";

        var options = new ConversationOptions("conversation")
        {
            Temperature = 0.7
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
                        Name = "TranslationRefiner",
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
