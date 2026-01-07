using Dapr.AI.Conversation;
using Dapr.AI.Conversation.Extensions;
using Dapr.AI.Conversation.ConversationRoles;
using Dapr.Workflow;
using System.Text.Json;
using Microsoft.VisualBasic;
using System.Runtime.InteropServices;
using System.Xml;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.ComponentModel.DataAnnotations;

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
                        Name = "TranslationRefiner",
                        Content = [new MessageContent(userPrompt)]
                    }
                })
            ],
            options);
        
        Console.WriteLine($"LOG RefineTranslationActivity response: {response.Outputs.First().Choices.First().Message.Content}");

        JsonElement json;
        try
        {
            json = JsonSerializer.Deserialize<JsonElement>(
                response.Outputs.First().Choices.First().Message.Content);
        }
        catch (JsonException ex)
        {
            var jsonString = ParseJsonString(response.Outputs.First().Choices.First().Message.Content);
            json = JsonSerializer.Deserialize<JsonElement>(jsonString);
        }
        
        return new Translation(
            input.Iteration,
            json.GetProperty("translation").GetString()!,
            json.GetProperty("reasoning").GetString()!,
            DateTime.UtcNow
        );
    }

    private string ParseJsonString(string input)
    {
        input = input.Trim();
        
        // Remove markdown code blocks if present
        if (input.StartsWith("```json") || input.StartsWith("```"))
        {
            var lines = input.Split('\n');
            input = string.Join('\n', lines.Skip(1));
        }
        if (input.EndsWith("```"))
        {
            var lastIndex = input.LastIndexOf("```");
            input = input.Substring(0, lastIndex);
        }
        input = input.Trim();
        
        // Ensure proper JSON object enclosure
        if (!input.StartsWith("{"))
        {
            input = "{" + input;
        }
        if (!input.EndsWith("}"))
        {
            input = input + "}";
        }
        
        // Process each line to ensure string values are properly quoted
        var lines2 = input.Split('\n');
        for (int i = 0; i < lines2.Length; i++)
        {
            var line = lines2[i];
            var trimmedLine = line.TrimStart();
            
            // Skip lines that are just braces or empty
            if (trimmedLine == "{" || trimmedLine == "}" || string.IsNullOrWhiteSpace(trimmedLine))
            {
                continue;
            }
            
            // Find the colon separator
            var colonIndex = trimmedLine.IndexOf(':');
            if (colonIndex > 0)
            {
                var key = trimmedLine.Substring(0, colonIndex).Trim();
                var valueAndRest = trimmedLine.Substring(colonIndex + 1).Trim();
                
                // Check if there's a trailing comma
                var hasComma = valueAndRest.EndsWith(",");
                var value = hasComma ? valueAndRest.Substring(0, valueAndRest.Length - 1).Trim() : valueAndRest;
                
                // If value doesn't start with a quote, it needs to be quoted
                if (!value.StartsWith("\""))
                {
                    value = "\"" + value;
                }
                
                // If value doesn't end with a quote, add it
                if (!value.EndsWith("\""))
                {
                    value = value + "\"";
                }
                
                // Reconstruct the line with proper indentation
                var indent = line.Length - trimmedLine.Length;
                lines2[i] = new string(' ', indent) + key + ": " + value + (hasComma ? "," : "");
            }
        }
        
        return string.Join('\n', lines2);
    }
}
