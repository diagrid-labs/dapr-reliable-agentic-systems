using Dapr.AI.Conversation;
using Dapr.AI.Conversation.Extensions;
using Dapr.AI.Conversation.ConversationRoles;
using Dapr.Workflow;
using Google.Protobuf.WellKnownTypes;
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
        
        var systemPrompt = @"You are an expert xenolinguist refining a translation based on
detailed editorial feedback. Your goal is to address the specific weaknesses identified
while maintaining the strengths of the current translation.

JSON structure that describes the fields:
{
  ""translation"": ""<improved translated text>"",
  ""reasoning"": ""<explanation of changes made to address feedback>""
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

Provide an improved translation that addresses the weaknesses while preserving the strengths.";

        var options = new ConversationOptions("conversation")
        {
            Temperature = 0.75,
            ResponseFormat = GetResponseFormat()
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

    private static Struct GetResponseFormat()
    {
        var stringType = new Struct();
        stringType.Fields.Add("type", Value.ForString("string"));

        var properties = new Struct();
        properties.Fields.Add("translation", Value.ForStruct(stringType));
        properties.Fields.Add("reasoning", Value.ForStruct(stringType));

        var responseFormat = new Struct();
        responseFormat.Fields.Add("type", Value.ForString("object"));
        responseFormat.Fields.Add("properties", Value.ForStruct(properties));
        responseFormat.Fields.Add("required", Value.ForList(
            Value.ForString("translation"),
            Value.ForString("reasoning")));

        return responseFormat;
    }
}
