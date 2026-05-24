using Dapr.AI.Conversation;
using Dapr.AI.Conversation.Extensions;
using Dapr.AI.Conversation.ConversationRoles;
using Dapr.Workflow;
using System.Text.Json;

public class EvaluateTranslationActivity : WorkflowActivity<EvaluateInput, Evaluation>
{
    private readonly DaprConversationClient _conversationClient;
    
    public EvaluateTranslationActivity(DaprConversationClient conversationClient)
    {
        _conversationClient = conversationClient;
    }
    
    public override async Task<Evaluation> RunAsync(
        WorkflowActivityContext context, 
        EvaluateInput input)
    {
        var systemPrompt = @"You are a senior translation evaluator for the Galactic Linguistic 
Institute. Your job is to provide detailed, constructive feedback on alien language translations 
intended for diplomatic and scientific use.

Evaluate translations on:
1. ACCURACY (0-10): How faithful is the translation to the original meaning?
2. CULTURAL NUANCE (0-10): Are cultural concepts and context preserved?
3. IDIOMATIC QUALITY (0-10): Does it read naturally in English?
4. OVERALL QUALITY (0-10): Holistic assessment

For each translation, provide:
- Numeric scores
- Specific strengths (what was done well)
- Specific weaknesses (what needs improvement)
- Detailed actionable feedback for refinement
- Whether it meets publication standards (overall >= 8.0 and no major flaws)";

        var userPrompt = $@"Evaluate this translation:

Original {input.OriginalText.AlienSpecies} Text: {input.OriginalText.OriginalText}
Context: {input.OriginalText.Context}
Cultural Notes: {input.OriginalText.CulturalNotes}

Translation (Iteration {input.CurrentTranslation.IterationNumber}):
{input.CurrentTranslation.TranslatedText}

Translator's Reasoning:
{input.CurrentTranslation.TranslatorReasoning}

Respond **only** with valid JSON.
Do not include explanations, comments, or text outside the JSON object.
Ensure the JSON is syntactically correct and can be parsed without errors.
Use double quotes around all keys and string values.
Use opening and closing curly braces.

JSON structure that describes the fields:
{{
  ""accuracyScore"": <0-10 numeric score>,
  ""culturalNuanceScore"": <0-10 numeric score>,
  ""idiomaticScore"": <0-10 numeric score>,
  ""overallQuality"": <0-10 numeric score>,
  ""strengths"": [""<specific strength 1>"", ""<specific strength 2>""],
  ""weaknesses"": [""<specific weakness 1>"", ""<specific weakness 2>""],
  ""detailedFeedback"": ""<comprehensive actionable feedback>"",
  ""meetsStandards"": <true or false boolean>
}}

Example:
{{
  ""accuracyScore"": 8.5,
  ""culturalNuanceScore"": 7.0,
  ""idiomaticScore"": 9.0,
  ""overallQuality"": 8.0,
  ""strengths"": [""Excellent handling of formal greeting conventions"", ""Natural English phrasing"", ""Preserved philosophical references""],
  ""weaknesses"": [""IDIC reference could be more explicit"", ""Cultural context of longevity greeting slightly understated""],
  ""detailedFeedback"": ""The translation successfully captures the formal diplomatic tone and core meaning. However, the IDIC philosophy reference would benefit from more explicit treatment to ensure non-Vulcan readers understand its significance. Consider expanding the greeting's emphasis on longevity as a core Vulcan value."",
  ""meetsStandards"": true
}}";

        var options = new ConversationOptions("conversation")
        {
            Temperature = 0.3
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
                        Name = "TranslationEvaluator",
                        Content = [new MessageContent(userPrompt)]
                    }
                })
            ],
            options);
        
        var json = JsonSerializer.Deserialize<JsonElement>(
            response.Outputs.First().Choices.First().Message.Content);
        
        return new Evaluation(
            input.CurrentTranslation.IterationNumber,
            json.GetProperty("accuracyScore").GetDouble(),
            json.GetProperty("culturalNuanceScore").GetDouble(),
            json.GetProperty("idiomaticScore").GetDouble(),
            json.GetProperty("overallQuality").GetDouble(),
            JsonSerializer.Deserialize<List<string>>(
                json.GetProperty("strengths").GetRawText())!,
            JsonSerializer.Deserialize<List<string>>(
                json.GetProperty("weaknesses").GetRawText())!,
            json.GetProperty("detailedFeedback").GetString()!,
            json.GetProperty("meetsStandards").GetBoolean()
        );
    }
}
