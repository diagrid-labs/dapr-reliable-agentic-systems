# Plan 5: Evaluator-Optimizer - Alien Language Translation Refinement

## Overview
This demo implements an **evaluator-optimizer workflow** using Dapr Workflow to help Captain Picard craft perfect diplomatic communications during first contact with new alien civilizations. The system generates an initial message with Data's assistance, then iteratively evaluates and refines it through multiple rounds until it meets Picard's exacting diplomatic standards and Starfleet protocols.

## Sci-Fi Use Case: Captain Picard's Diplomatic Communication Protocol
You're building a backend system for Captain Jean-Luc Picard to craft critical diplomatic communications during first contact scenarios. As a seasoned diplomat and archaeologist, Picard understands that first contact messages must be perfect. Data assists with translation while Counselor Troi (implied) evaluates emotional and cultural nuances:
1. **Generate** an initial translation and diplomatic response with Data's linguistic databases
2. **Evaluate** communication quality (accuracy, cultural sensitivity, diplomatic tone, Prime Directive compliance)
3. **Provide feedback** on what's missing, culturally insensitive, or diplomatically inappropriate
4. **Refine** the communication based on detailed feedback
5. **Iterate** until the message meets Starfleet diplomatic standards and Picard's exacting requirements

This is analogous to Picard's careful approach to diplomacy—going through multiple drafts, considering cultural implications, and polishing every word before transmission.

## Architecture

### Services
1. **TranslationAPI** - ASP.NET Core Web API (Entry point)
2. **EvaluatorOptimizerWorkflow** - Main iterative refinement workflow
3. **TranslatorActivity** - Generates and refines translations
4. **EvaluatorActivity** - Provides detailed quality assessment and feedback

### Components Used
- **Dapr Workflow** - Orchestrates the evaluation-optimization loop
- **Dapr State Management** - Stores translation iterations and history
- **Dapr Conversation API** - Powers both translator and evaluator

## Detailed Implementation Plan

### Step 1: Project Setup
```
/AlienTranslator
  - Program.cs
  - local.http
  - appsettings.json
  - dapr.yaml
  /Workflows
    - EvaluatorOptimizerWorkflow.cs
  /Activities
    - TranslateActivity.cs
    - EvaluateTranslationActivity.cs
    - RefineTranslationActivity.cs
  /Models
    - AlienText.cs
    - Translation.cs
    - Evaluation.cs
    - TranslationResult.cs
  /Resources
    - statestore.yaml
    - conversation.yaml
```

### Step 2: Core Models
**AlienText.cs**
```csharp
public record AlienText(
    string TextId,
    string AlienSpecies,
    string OriginalText,
    string Context, // "Diplomatic", "Scientific", "Literary", "Casual"
    Dictionary<string, string> KnownVocabulary, // word -> meaning
    string CulturalNotes
);

public record Translation(
    int IterationNumber,
    string TranslatedText,
    string TranslatorReasoning,
    DateTime Timestamp
);

public record Evaluation(
    int IterationNumber,
    double AccuracyScore, // 0-10
    double CulturalNuanceScore, // 0-10
    double IdiomaticScore, // 0-10
    double OverallQuality, // 0-10
    List<string> Strengths,
    List<string> Weaknesses,
    string DetailedFeedback,
    bool MeetsStandards
);

public record TranslationResult(
    string TextId,
    List<Translation> Iterations,
    List<Evaluation> Evaluations,
    Translation FinalTranslation,
    Evaluation FinalEvaluation,
    int TotalIterations,
    string ImprovementSummary
);
```

### Step 3: Evaluator-Optimizer Workflow
**EvaluatorOptimizerWorkflow.cs**
```csharp
public class EvaluatorOptimizerWorkflow : Workflow<AlienText, TranslationResult>
{
    private const int MAX_ITERATIONS = 5;
    private const double QUALITY_THRESHOLD = 8.0;
    
    public override async Task<TranslationResult> RunAsync(
        WorkflowContext context, 
        AlienText input)
    {
        var iterations = new List<Translation>();
        var evaluations = new List<Evaluation>();
        
        // Initial translation (iteration 0)
        var currentTranslation = await context.CallActivityAsync<Translation>(
            nameof(TranslateActivity),
            new TranslateInput(input, null, 0));
        
        iterations.Add(currentTranslation);
        
        // Evaluation-optimization loop
        for (int i = 0; i < MAX_ITERATIONS; i++)
        {
            // Evaluate current translation
            var evaluation = await context.CallActivityAsync<Evaluation>(
                nameof(EvaluateTranslationActivity),
                new EvaluateInput(input, currentTranslation));
            
            evaluations.Add(evaluation);
            
            // Check if quality threshold met
            if (evaluation.MeetsStandards && evaluation.OverallQuality >= QUALITY_THRESHOLD)
            {
                // Success! Translation is good enough
                return new TranslationResult(
                    input.TextId,
                    iterations,
                    evaluations,
                    currentTranslation,
                    evaluation,
                    i + 1,
                    GenerateImprovementSummary(evaluations)
                );
            }
            
            // Check if we've hit max iterations
            if (i == MAX_ITERATIONS - 1)
            {
                // Return best effort translation
                return new TranslationResult(
                    input.TextId,
                    iterations,
                    evaluations,
                    currentTranslation,
                    evaluation,
                    MAX_ITERATIONS,
                    GenerateImprovementSummary(evaluations) + 
                    " (Max iterations reached - manual review recommended)"
                );
            }
            
            // Refine translation based on feedback
            var refinedTranslation = await context.CallActivityAsync<Translation>(
                nameof(RefineTranslationActivity),
                new RefineInput(
                    input, 
                    currentTranslation, 
                    evaluation, 
                    i + 1));
            
            iterations.Add(refinedTranslation);
            currentTranslation = refinedTranslation;
        }
        
        // Should never reach here due to loop logic, but required for compilation
        throw new InvalidOperationException("Unexpected workflow state");
    }
    
    private string GenerateImprovementSummary(List<Evaluation> evaluations)
    {
        if (evaluations.Count < 2)
            return "Initial translation completed";
        
        var firstScore = evaluations.First().OverallQuality;
        var lastScore = evaluations.Last().OverallQuality;
        var improvement = lastScore - firstScore;
        
        return $"Quality improved from {firstScore:F1} to {lastScore:F1} " +
               $"(+{improvement:F1} points over {evaluations.Count} evaluations)";
    }
}

public record TranslateInput(AlienText Text, Evaluation? PreviousFeedback, int Iteration);
public record EvaluateInput(AlienText OriginalText, Translation CurrentTranslation);
public record RefineInput(AlienText Text, Translation Current, Evaluation Feedback, int Iteration);
```

### Step 4: Translator Activities

> **Note**: All translator and evaluator activities use `DaprConversationClient` with constructor injection:
> ```csharp
> using Dapr.AI.Conversation;
> using Dapr.AI.Conversation.Extensions;
> 
> private readonly DaprConversationClient _conversationClient;
> 
> public YourActivity(DaprConversationClient conversationClient)
> {
>     _conversationClient = conversationClient;
> }
> ```
> Use `ConversationOptions("conversation")` with model and temperature, then `ConverseAsync()` with message collections.

**TranslateActivity.cs** (Initial Translation)
```csharp
public class TranslateActivity : WorkflowActivity<TranslateInput, Translation>
{
    private readonly DaprClient _daprClient;
    
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

Provide both the translation and your reasoning for key choices.";

        var userPrompt = $@"Translate this {input.Text.AlienSpecies} text to English:

Original Text: {input.Text.OriginalText}
Context: {input.Text.Context}
Cultural Notes: {input.Text.CulturalNotes}

Known Vocabulary:
{string.Join("\n", input.Text.KnownVocabulary.Select(kv => $"- {kv.Key}: {kv.Value}"))}

Provide your translation and explain your reasoning for important translation choices.";

        var conversationRequest = new
        {
            model = "gpt-4o",
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            response_format = new { type = "json_object" }
        };
        
        var response = await _daprClient.InvokeMethodAsync<object, ConversationResponse>(
            HttpMethod.Post,
            "conversation",
            "v1/chat/completions",
            conversationRequest);
        
        var json = JsonSerializer.Deserialize<JsonElement>(
            response.Choices[0].Message.Content);
        
        return new Translation(
            input.Iteration,
            json.GetProperty("translation").GetString(),
            json.GetProperty("reasoning").GetString(),
            DateTime.UtcNow
        );
    }
}
```

**RefineTranslationActivity.cs** (Iterative Refinement)
```csharp
public class RefineTranslationActivity : WorkflowActivity<RefineInput, Translation>
{
    private readonly DaprClient _daprClient;
    
    public override async Task<Translation> RunAsync(
        WorkflowActivityContext context, 
        RefineInput input)
    {
        var systemPrompt = @"You are an expert xenolinguist refining a translation based on 
detailed editorial feedback. Your goal is to address the specific weaknesses identified 
while maintaining the strengths of the current translation.";

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

        var conversationRequest = new
        {
            model = "gpt-4o",
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            response_format = new { type = "json_object" }
        };
        
        var response = await _daprClient.InvokeMethodAsync<object, ConversationResponse>(
            HttpMethod.Post,
            "conversation",
            "v1/chat/completions",
            conversationRequest);
        
        var json = JsonSerializer.Deserialize<JsonElement>(
            response.Choices[0].Message.Content);
        
        return new Translation(
            input.Iteration,
            json.GetProperty("translation").GetString(),
            json.GetProperty("reasoning").GetString(),
            DateTime.UtcNow
        );
    }
}
```

### Step 5: Evaluator Activity

**EvaluateTranslationActivity.cs**
```csharp
public class EvaluateTranslationActivity : WorkflowActivity<EvaluateInput, Evaluation>
{
    private readonly DaprClient _daprClient;
    
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

Provide comprehensive evaluation in JSON format:
{{
  ""accuracyScore"": 0-10,
  ""culturalNuanceScore"": 0-10,
  ""idiomaticScore"": 0-10,
  ""overallQuality"": 0-10,
  ""strengths"": [""strength 1"", ""strength 2""],
  ""weaknesses"": [""weakness 1"", ""weakness 2""],
  ""detailedFeedback"": ""comprehensive feedback for improvement"",
  ""meetsStandards"": true/false
}}";

        var conversationRequest = new
        {
            model = "gpt-4o", // Use powerful model for nuanced evaluation
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            response_format = new { type = "json_object" }
        };
        
        var response = await _daprClient.InvokeMethodAsync<object, ConversationResponse>(
            HttpMethod.Post,
            "conversation",
            "v1/chat/completions",
            conversationRequest);
        
        var json = JsonSerializer.Deserialize<JsonElement>(
            response.Choices[0].Message.Content);
        
        return new Evaluation(
            input.CurrentTranslation.IterationNumber,
            json.GetProperty("accuracyScore").GetDouble(),
            json.GetProperty("culturalNuanceScore").GetDouble(),
            json.GetProperty("idiomaticScore").GetDouble(),
            json.GetProperty("overallQuality").GetDouble(),
            JsonSerializer.Deserialize<List<string>>(
                json.GetProperty("strengths").GetRawText()),
            JsonSerializer.Deserialize<List<string>>(
                json.GetProperty("weaknesses").GetRawText()),
            json.GetProperty("detailedFeedback").GetString(),
            json.GetProperty("meetsStandards").GetBoolean()
        );
    }
}
```

### Step 6: API Endpoints
**Program.cs**
```csharp
using Dapr.Client;
using Dapr.Workflow;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDaprClient();
builder.Services.AddDaprConversationClient();
builder.Services.AddDaprWorkflow(options =>
{
    options.RegisterWorkflow<EvaluatorOptimizerWorkflow>();
    options.RegisterActivity<TranslateActivity>();
    options.RegisterActivity<RefineTranslationActivity>();
    options.RegisterActivity<EvaluateTranslationActivity>();
});

var app = builder.Build();

// Start translation with iterative refinement
app.MapPost("/translate", async (
    AlienText text,
    DaprWorkflowClient workflowClient,
    DaprClient daprClient) =>
{
    var instanceId = $"translation-{text.TextId}";
    
    // Store original text
    await daprClient.SaveStateAsync(
        "statestore",
        $"text-{text.TextId}",
        text);
    
    // Start evaluator-optimizer workflow
    await workflowClient.ScheduleNewWorkflowAsync(
        nameof(EvaluatorOptimizerWorkflow),
        instanceId,
        text);
    
    return Results.Accepted($"/translate/{instanceId}", new { instanceId });
});

// Get translation status and results
app.MapGet("/translate/{instanceId}", async (
    string instanceId,
    DaprWorkflowClient workflowClient) =>
{
    var state = await workflowClient.GetWorkflowStateAsync(instanceId);
    
    if (state == null)
        return Results.NotFound();
    
    var result = state.ReadOutputAs<TranslationResult>();
    
    return Results.Ok(new
    {
        instanceId,
        status = state.RuntimeStatus.ToString(),
        result,
        createdAt = state.CreatedAt,
        completedAt = state.LastUpdatedAt
    });
});

// Get specific iteration details
app.MapGet("/translate/{instanceId}/iteration/{iterationNumber}", async (
    string instanceId,
    int iterationNumber,
    DaprWorkflowClient workflowClient) =>
{
    var state = await workflowClient.GetWorkflowStateAsync(instanceId);
    
    if (state == null)
        return Results.NotFound();
    
    var result = state.ReadOutputAs<TranslationResult>();
    var translation = result.Iterations.FirstOrDefault(i => i.IterationNumber == iterationNumber);
    var evaluation = result.Evaluations.FirstOrDefault(e => e.IterationNumber == iterationNumber);
    
    if (translation == null)
        return Results.NotFound();
    
    return Results.Ok(new
    {
        translation,
        evaluation
    });
});

// Get a specific translation by text ID
app.MapGet("/translations/{textId}", async (
    string textId,
    DaprClient daprClient) =>
{
    var translation = await daprClient.GetStateAsync<AlienText>(
        "statestore",
        textId);
    
    if (translation == null)
        return Results.NotFound();
    
    return Results.Ok(translation);
});

app.Run();
```

### Step 7: Dapr MultiApp Run
**dapr.yaml**
```yaml
version: 1
common:
  resourcesPath: Resources
  appLogDestination: fileAndConsole
  daprdLogDestination: fileAndConsole
apps:
  - appID: diplomatic-comms-app
    appDirPath: /
    appPort: 5500
    daprHTTPPort: 3550
    command: ["dotnet", "run"]
```

### Step 8: Local HTTP Tests
**local.http**
```http
### Translate a diplomatic message from the Vulcans
POST http://localhost:5000/translate
Content-Type: application/json

{
  "textId": "VULN-DIPL-001",
  "alienSpecies": "Vulcan",
  "originalText": "Dif-tor heh smusma. Kal-tor pan shar. Taluhk nash-veh k'dular.",
  "context": "Diplomatic",
  "knownVocabulary": {
    "Dif-tor heh smusma": "Live long and prosper (formal greeting)",
    "Kal-tor": "infinite diversity",
    "Taluhk": "cherish/treasure"
  },
  "culturalNotes": "Vulcans value logic and precision. Greetings often reference longevity and prosperity. IDIC (Infinite Diversity in Infinite Combinations) is a core philosophy."
}

### Translate a Klingon battle poem
POST http://localhost:5000/translate
Content-Type: application/json

{
  "textId": "KLING-LIT-042",
  "alienSpecies": "Klingon",
  "originalText": "Qapla'! qaStaHvIS wa' ram loS SaD Hugh SIjlaH qetbogh loD.",
  "context": "Literary",
  "knownVocabulary": {
    "Qapla'": "Success (battle cry)",
    "qaStaHvIS": "while/during",
    "SIjlaH": "can imagine",
    "qetbogh": "running"
  },
  "culturalNotes": "Klingon poetry emphasizes honor, battle prowess, and glory in death. Metaphors often involve warfare and physical strength. Direct translation may miss warrior ethos."
}

### Translate casual Romulan conversation
POST http://localhost:5000/translate
Content-Type: application/json

{
  "textId": "ROM-CAS-015",
  "alienSpecies": "Romulan",
  "originalText": "Jolan'tru. Mnei sahe nnea. Lhhei s'dena khir.",
  "context": "Casual",
  "knownVocabulary": {
    "Jolan'tru": "hello (casual)",
    "Mnei sahe": "my friend",
    "Lhhei": "until",
    "khir": "stars"
  },
  "culturalNotes": "Romulans value secrecy and cunning. Even casual speech contains layers of meaning. References to stars and night are common in farewells."
}

### Get translation result
GET http://localhost:5000/translate/translation-VULN-DIPL-001

### Get specific iteration
GET http://localhost:5000/translate/translation-VULN-DIPL-001/iteration/2

### Get translation history
GET http://localhost:5000/translations/history
```

## Benefits of Evaluator-Optimizer Pattern

### 1. **Iterative Quality Improvement**
Each cycle improves the translation:
- Initial translation may miss nuances
- Evaluator identifies specific weaknesses
- Refinement addresses those weaknesses
- Quality scores increase over iterations
- Demonstrable improvement trajectory

### 2. **Measurable Quality Standards**
Clear criteria for completion:
- Numeric scores provide objective metrics
- Quality threshold defines "good enough"
- Can track improvement quantitatively
- Enables data-driven stopping conditions

### 3. **Specialized Roles**
Separation of concerns:
- **Translator LLM**: Focuses on creating translation
- **Evaluator LLM**: Provides critical assessment
- Different prompts optimize for different goals
- Evaluator can be more critical/detailed

### 4. **Detailed Feedback Loop**
Actionable guidance for refinement:
- Specific strengths to preserve
- Specific weaknesses to address
- Detailed explanatory feedback
- Translator can target improvements

### 5. **Human-Like Workflow**
Mimics professional translation process:
- Draft → Review → Revise → Review → Publish
- Multiple rounds of editorial feedback
- Iterative refinement to high quality
- Natural pattern for complex tasks

### 6. **Graceful Degradation**
Handles difficult translations:
- Max iterations prevents infinite loops
- Returns best effort if threshold not met
- Flags for manual review when needed
- Partial success still valuable

## Drawbacks of Evaluator-Optimizer Pattern

### 1. **High Latency**
Sequential refinement cycles:
- Each iteration: translate → evaluate (2 LLM calls)
- 5 iterations = 10+ LLM calls
- 30-60 seconds total for complex translations
- User must wait for entire loop

### 2. **Expensive**
Multiple LLM calls per translation:
- Using GPT-4 for both translator and evaluator
- 5 iterations × 2 calls = 10 high-cost calls
- Simple translations still pay full cost
- No way to predict iteration count upfront

### 3. **Potential for Minimal Improvement**
Diminishing returns:
- Later iterations may make only tiny improvements
- Evaluator might not have actionable feedback
- Translator might not know how to address feedback
- Can "thrash" without meaningful progress

### 4. **Subjectivity Issues**
Quality evaluation is inherently subjective:
- Evaluator's scores may be inconsistent
- Different evaluator prompts yield different scores
- "Meets standards" threshold is arbitrary
- Hard to validate evaluator's judgment

### 5. **Context Window Limitations**
Accumulating history:
- Each iteration needs previous translation + feedback
- Context grows with each cycle
- May hit token limits on long texts
- Requires careful context management

### 6. **Lack of True Understanding**
LLMs can't truly verify translation accuracy:
- Evaluator doesn't "know" the alien language
- Can't verify semantic correctness
- Relies on reasoning about plausibility
- May miss subtle errors

## When to Use This Pattern

**Use evaluator-optimizer when:**
- Quality is more important than speed
- Iterative refinement demonstrably improves results
- LLMs can provide meaningful feedback on their own output
- Clear evaluation criteria exist
- Tasks benefit from editorial-style review

**Good use cases:**
- Literary translation
- Document polishing and editing
- Code review and refinement
- Creative content improvement
- Complex analysis requiring peer review

**Avoid evaluator-optimizer when:**
- Speed is critical
- First attempt is usually good enough
- Evaluation criteria are unclear
- Feedback loop doesn't improve quality
- Cost constraints are tight

## Running the Demo

### Prerequisites
```powershell
# Install Dapr CLI
powershell -Command "iwr -useb https://raw.githubusercontent.com/dapr/cli/master/install/install.ps1 | iex"

# Initialize Dapr
dapr init

# Run the Diagrid Dashboard
docker run -p 8080:8080 ghcr.io/diagridio/diagrid-dashboard:latest
```

### Run the application
```powershell
# Run with Dapr MultiApp Run
dapr run -f .
```

### Test the workflow
Use VSCode REST Client with `local.http` to translate alien texts and observe iterative refinement.

## Conclusion
The evaluator-optimizer pattern is ideal for tasks requiring iterative refinement to meet high quality standards. By separating translation and evaluation into distinct LLM roles, the system mimics the human editorial process—producing progressively better translations through structured feedback loops. While more expensive and slower than single-pass approaches, the quality improvements justify the cost for important diplomatic and scientific translations. Dapr Workflow's durability ensures the entire refinement process can survive interruptions and be audited later.
