using Dapr.AI.Conversation;
using Dapr.AI.Conversation.Extensions;
using Dapr.AI.Conversation.ConversationRoles;
using Dapr.Workflow;
using Google.Protobuf.WellKnownTypes;
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

Keep `detailedFeedback` <=100 words. `strengths` and `weaknesses` <=5 items each, each item <=20 words. Return only the JSON object with no surrounding prose.";

        var options = new ConversationOptions("conversation")
        {
            Temperature = 0.3,
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

    private static Struct GetResponseFormat()
    {
        var stringType = new Struct();
        stringType.Fields.Add("type", Value.ForString("string"));

        var numberType = new Struct();
        numberType.Fields.Add("type", Value.ForString("number"));

        var booleanType = new Struct();
        booleanType.Fields.Add("type", Value.ForString("boolean"));

        var stringArrayType = new Struct();
        stringArrayType.Fields.Add("type", Value.ForString("array"));
        stringArrayType.Fields.Add("items", Value.ForStruct(stringType));

        var properties = new Struct();
        properties.Fields.Add("accuracyScore", Value.ForStruct(numberType));
        properties.Fields.Add("culturalNuanceScore", Value.ForStruct(numberType));
        properties.Fields.Add("idiomaticScore", Value.ForStruct(numberType));
        properties.Fields.Add("overallQuality", Value.ForStruct(numberType));
        properties.Fields.Add("strengths", Value.ForStruct(stringArrayType));
        properties.Fields.Add("weaknesses", Value.ForStruct(stringArrayType));
        properties.Fields.Add("detailedFeedback", Value.ForStruct(stringType));
        properties.Fields.Add("meetsStandards", Value.ForStruct(booleanType));

        var responseFormat = new Struct();
        responseFormat.Fields.Add("type", Value.ForString("object"));
        responseFormat.Fields.Add("properties", Value.ForStruct(properties));
        responseFormat.Fields.Add("required", Value.ForList(
            Value.ForString("accuracyScore"),
            Value.ForString("culturalNuanceScore"),
            Value.ForString("idiomaticScore"),
            Value.ForString("overallQuality"),
            Value.ForString("strengths"),
            Value.ForString("weaknesses"),
            Value.ForString("detailedFeedback"),
            Value.ForString("meetsStandards")));

        return responseFormat;
    }
}
