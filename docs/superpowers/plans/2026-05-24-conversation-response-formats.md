# Dapr Conversation API ResponseFormat Migration — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrate every activity in `AlienTranslator`, `GalacticAnomalyClassifier`, `SpaceColonyPlanner`, `SpaceDebrisAgent`, and `StarshipDiagnostics` that currently coerces JSON output via prompt text over to the Dapr Conversation API's structured `ResponseFormat`, following the `AnalyzeHullActivityWithConversation.cs` reference. `AnomalyAnalysis` has no JSON-output activities and is out of scope.

**Architecture:** Each activity declares a `Google.Protobuf.WellKnownTypes.Struct` JSON-schema and passes it via `ConversationOptions.ResponseFormat`. The system prompt is trimmed: the embedded JSON skeleton, the example block, and the "Respond only with valid JSON / use double quotes / opening and closing curly braces" boilerplate go away — they are now enforced structurally. Where multiple activities in one project share an output type (StarshipDiagnostics scanners → `ScanResult`; SpaceColonyPlanner workers → `StructurePlan`), the schema is extracted to a single `*Schema.cs` static helper so all activities in the group share one definition.

**Tech Stack:** .NET 9, `Dapr.AI.Conversation`, `Google.Protobuf.WellKnownTypes`, Dapr Workflows.

**Scope summary (26 activities total):**

| Project | # activities | Notes |
|---|---|---|
| AlienTranslator | 3 | TranslateActivity, EvaluateTranslationActivity, RefineTranslationActivity. Also delete `JsonUtils` fallback. |
| GalacticAnomalyClassifier | 6 | ClassifyAnomalyActivity + 5 worker analyzers; the 5 workers each have a unique shape so no shared schema. |
| SpaceColonyPlanner | 10 | 3 in `Activities/Analysis/`, 7 in `Activities/Workers/`. The 7 workers share `StructurePlan`. |
| SpaceDebrisAgent | 2 | AgentReasoningActivity (Agent/), ScanDebrisFieldActivity (Tools/). |
| StarshipDiagnostics | 5 | All scanners share `ScanResult`. |

**Verification approach:** these activities call a real LLM; there are no mockable unit tests for the parse path. Per-task verification is:
1. `dotnet build` of the affected `*.ApiService` project must succeed.
2. The corresponding workflow is invoked end-to-end via the project's `local.http` (or DemoTime trigger) and the parsed result is logged/inspected to confirm every schema field deserializes.

Where this plan calls for "smoke-test the workflow", the engineer should already have a local Dapr/Aspire environment running (`dotnet run` on the AppHost). If not, set that up first using the project's existing README before running these tasks.

**Cross-cutting style rules (apply to every migration):**

1. Add `using Google.Protobuf.WellKnownTypes;` to every activity file you touch (do not use the fully-qualified name inline — the reference does, but for files declaring ~5 fields it becomes unreadable).
2. Add a `private static Google.Protobuf.WellKnownTypes.Struct GetResponseFormat()` method (or use the shared schema class — see Task 2 for the conventions).
3. Strip from the system prompt:
   - `Respond **only** with valid JSON.`
   - `Do not include explanations, comments, or text outside the JSON object.`
   - `Ensure the JSON is syntactically correct and can be parsed without errors.`
   - `Use double quotes around all keys and string values.`
   - `Use opening and closing curly braces.`
   - the `JSON structure that describes the fields: { ... }` block
   - the `Example: { ... }` block
   - any closing instruction like `Return JSON with: ...`
4. **Keep** the role description and the domain instructions (the "you are a hull integrity scanner AI, analyze for micrometeorite impacts..." part). The schema enforces *shape*; the prompt still drives *content*.
5. Set `PromptCacheRetention = TimeSpan.FromMinutes(15)` on `ConversationOptions` to match the reference.
6. Remove every `Console.WriteLine($"... Response: {response...}");` log line you encounter in activities you migrate. The structured response no longer needs raw-text debugging.
7. Do not change the activity's public signature (`WorkflowActivity<TIn, TOut>`) or the result model. Only the prompt, the `ConversationOptions`, and the deserialization path may change.
8. After deserialization, prefer the existing pattern (deserialize to `JsonElement`, pull properties) — the schema guarantees the shape so the field accessors no longer need null/typed defensiveness, but match the surrounding style.

---

## File Structure

**New files (created in this plan):**

- `StarshipDiagnostics/StarshipDiagnostics.ApiService/Activities/Scanners/ScanResultSchema.cs` — shared `Struct` for all 5 scanners.
- `SpaceColonyPlanner/SpaceColonyPlanner.ApiService/Activities/Workers/StructurePlanSchema.cs` — shared `Struct` for all 7 workers.

**Modified files (one per migrated activity, listed per-task below):**

- 3 in `AlienTranslator/AlienTranslator.ApiService/Activities/`
- 6 in `GalacticAnomalyClassifier/GalacticAnomalyClassifier.ApiService/Activities/`
- 10 in `SpaceColonyPlanner/SpaceColonyPlanner.ApiService/Activities/{Analysis,Workers}/`
- 2 in `SpaceDebrisAgent/SpaceDebrisAgent.ApiService/Activities/{Agent,Tools}/`
- 5 in `StarshipDiagnostics/StarshipDiagnostics.ApiService/Activities/Scanners/`

**Deleted files:**

- `AlienTranslator/AlienTranslator.ApiService/Activities/JsonUtils.cs` — the bracket-matching fallback parser exists only because the model occasionally returned malformed JSON; structured output makes this dead code.

---

## Task 1: Baseline build verification

Confirm every project compiles before any changes so later build failures are unambiguously caused by this work.

**Files:** none modified.

- [ ] **Step 1: Build all five in-scope projects**

Run:

```bash
cd /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems
dotnet build AlienTranslator/AlienTranslator.ApiService/AlienTranslator.ApiService.csproj
dotnet build GalacticAnomalyClassifier/GalacticAnomalyClassifier.ApiService/GalacticAnomalyClassifier.ApiService.csproj
dotnet build SpaceColonyPlanner/SpaceColonyPlanner.ApiService/SpaceColonyPlanner.ApiService.csproj
dotnet build SpaceDebrisAgent/SpaceDebrisAgent.ApiService/SpaceDebrisAgent.ApiService.csproj
dotnet build StarshipDiagnostics/StarshipDiagnostics.ApiService/StarshipDiagnostics.ApiService.csproj
```

Expected: each `dotnet build` ends with `Build succeeded`. If a project fails to build at baseline, **stop**, fix that before continuing — do not assume the failure is pre-existing.

- [ ] **Step 2: Confirm the reference exists and matches the expected pattern**

Run:

```bash
grep -n "ResponseFormat" /Users/marcduiker/dev/diagrid-labs/dapr-workflow-versioning/EnterpriseDiagnostics/EnterpriseDiagnostics.ApiService/Activities/AnalyzeHullActivityWithConversation.cs
```

Expected: lines mentioning `ResponseFormat = GetResponseFormat()` and `private static Google.Protobuf.WellKnownTypes.Struct GetResponseFormat()`.

If the reference has moved or changed, sync with the user before continuing.

---

## Task 2: Establish schema-building conventions

This task does not write code — it documents the protobuf-Struct idioms the rest of the plan reuses. The engineer should read it and refer back when each subsequent task asks for a schema. No commit.

**Convention reference — copy-paste templates:**

```csharp
// === String field ===
var stringType = new Struct();
stringType.Fields.Add("type", Value.ForString("string"));

// === Integer field ===
var integerType = new Struct();
integerType.Fields.Add("type", Value.ForString("integer"));

// === Number (double) field ===
var numberType = new Struct();
numberType.Fields.Add("type", Value.ForString("number"));

// === Boolean field ===
var booleanType = new Struct();
booleanType.Fields.Add("type", Value.ForString("boolean"));

// === Array of strings ===
var stringArrayType = new Struct();
stringArrayType.Fields.Add("type", Value.ForString("array"));
stringArrayType.Fields.Add("items", Value.ForStruct(stringType));

// === Open-shape object (Dictionary<string, object>) ===
// JSON-Schema "object" with no declared properties acts as a free-form map.
var openObjectType = new Struct();
openObjectType.Fields.Add("type", Value.ForString("object"));

// === Object with declared properties ===
var props = new Struct();
props.Fields.Add("fieldA", Value.ForStruct(stringType));
props.Fields.Add("fieldB", Value.ForStruct(integerType));
var objectType = new Struct();
objectType.Fields.Add("type", Value.ForString("object"));
objectType.Fields.Add("properties", Value.ForStruct(props));
objectType.Fields.Add("required", Value.ForList(
    Value.ForString("fieldA"),
    Value.ForString("fieldB")));

// === Array of objects ===
var objectArrayType = new Struct();
objectArrayType.Fields.Add("type", Value.ForString("array"));
objectArrayType.Fields.Add("items", Value.ForStruct(objectType));

// === Array of numbers (e.g., position [x, y, z]) ===
var numberArrayType = new Struct();
numberArrayType.Fields.Add("type", Value.ForString("array"));
numberArrayType.Fields.Add("items", Value.ForStruct(numberType));
```

**Enum policy:** the reference does not constrain enum-like strings with a JSON-Schema `enum` array; it leaves the value as `"type": "string"` and trusts the prompt to mention valid values. This plan follows the reference. For every existing "LOW|MEDIUM|HIGH|CRITICAL"-style field, the prompt's domain section already names the values — leave that mention in place, just remove the JSON-skeleton block.

**Required vs. optional:** every property currently parsed by the activity is in practice required (the activity throws on missing fields). List every property in the schema's `required` array.

**`using` directive:** add `using Google.Protobuf.WellKnownTypes;` to every file you touch in this plan so the templates above compile as written.

No build step. No commit.

---

## Task 3: Migrate `AlienTranslator/Activities/TranslateActivity.cs`

**Files:**
- Modify: `AlienTranslator/AlienTranslator.ApiService/Activities/TranslateActivity.cs`

- [ ] **Step 1: Read the current file end-to-end**

Run:

```bash
sed -n '1,200p' /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems/AlienTranslator/AlienTranslator.ApiService/Activities/TranslateActivity.cs
```

Note the existing system-prompt structure and the deserialization block. Your edits must preserve everything outside the prompt boilerplate and the `ConversationOptions` construction.

- [ ] **Step 2: Add `using` directive**

Edit the top of the file to add `using Google.Protobuf.WellKnownTypes;`.

- [ ] **Step 3: Add `GetResponseFormat()` method**

At the bottom of the class, before the closing brace, add:

```csharp
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
```

- [ ] **Step 4: Wire the response format into `ConversationOptions`**

In `RunAsync`, change the `ConversationOptions` construction to:

```csharp
var options = new ConversationOptions("conversation")
{
    Temperature = 0.75,
    PromptCacheRetention = TimeSpan.FromMinutes(15),
    ResponseFormat = GetResponseFormat()
};
```

- [ ] **Step 5: Trim the system prompt**

Replace the entire `systemPrompt` string with a version that keeps only the role and domain instructions and drops the JSON-shape boilerplate. The exact replacement:

```csharp
var systemPrompt = @"You are an expert xenolinguist specializing in translating alien languages 
into clear, accurate English. Provide a translated text along with reasoning that explains 
key translation choices and cultural nuances.";
```

(If the existing role text in the file is more elaborate, keep it — only delete the "Respond only with valid JSON" / `JSON structure` / `Example:` blocks. The role wording above is a minimum-floor version.)

Likewise, strip the trailing `Return JSON with: translation (string), reasoning (string)` instruction from `userPrompt` if present.

- [ ] **Step 6: Remove debug logging**

Delete any `Console.WriteLine($"... Response: {response...}")` line in this activity.

- [ ] **Step 7: Build**

Run:

```bash
dotnet build /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems/AlienTranslator/AlienTranslator.ApiService/AlienTranslator.ApiService.csproj
```

Expected: `Build succeeded`.

- [ ] **Step 8: Commit**

```bash
cd /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems
git add AlienTranslator/AlienTranslator.ApiService/Activities/TranslateActivity.cs
git commit -m "refactor(AlienTranslator): use ResponseFormat for TranslateActivity"
```

---

## Task 4: Migrate `AlienTranslator/Activities/EvaluateTranslationActivity.cs`

**Files:**
- Modify: `AlienTranslator/AlienTranslator.ApiService/Activities/EvaluateTranslationActivity.cs`

- [ ] **Step 1: Add `using` directive**

Add `using Google.Protobuf.WellKnownTypes;` to the top.

- [ ] **Step 2: Add `GetResponseFormat()` method**

```csharp
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
```

Note: scores are doubles (0–10), so the schema uses `"number"`, not `"integer"`.

- [ ] **Step 3: Wire response format into `ConversationOptions`**

```csharp
var options = new ConversationOptions("conversation")
{
    Temperature = 0.3,
    PromptCacheRetention = TimeSpan.FromMinutes(15),
    ResponseFormat = GetResponseFormat()
};
```

- [ ] **Step 4: Trim the system prompt**

Strip the "Respond only with valid JSON" block, the `JSON structure` block, and the `Example` block from the system prompt. Keep the evaluator role description and the explanation of the four scoring dimensions (accuracy / cultural nuance / idiomatic quality / overall quality). The model still needs to know what to score; it just no longer needs to know how to format JSON.

- [ ] **Step 5: Remove debug logging**

Delete any `Console.WriteLine` lines that dump the response.

- [ ] **Step 6: Build & commit**

```bash
dotnet build /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems/AlienTranslator/AlienTranslator.ApiService/AlienTranslator.ApiService.csproj
cd /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems
git add AlienTranslator/AlienTranslator.ApiService/Activities/EvaluateTranslationActivity.cs
git commit -m "refactor(AlienTranslator): use ResponseFormat for EvaluateTranslationActivity"
```

---

## Task 5: Migrate `AlienTranslator/Activities/RefineTranslationActivity.cs` and delete `JsonUtils`

**Files:**
- Modify: `AlienTranslator/AlienTranslator.ApiService/Activities/RefineTranslationActivity.cs`
- Delete: `AlienTranslator/AlienTranslator.ApiService/Activities/JsonUtils.cs`

- [ ] **Step 1: Verify `JsonUtils` is referenced only by `RefineTranslationActivity`**

Run:

```bash
grep -rn "JsonUtils" /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems/AlienTranslator/
```

Expected: matches only in `JsonUtils.cs` and `RefineTranslationActivity.cs`. If anything else uses it, stop and re-scope this task.

- [ ] **Step 2: Add `using` directive**

Add `using Google.Protobuf.WellKnownTypes;`.

- [ ] **Step 3: Add `GetResponseFormat()` method**

Same shape as Task 3 (TranslateActivity) — `translation` + `reasoning`:

```csharp
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
```

- [ ] **Step 4: Wire `ConversationOptions`**

```csharp
var options = new ConversationOptions("conversation")
{
    Temperature = 0.75,
    PromptCacheRetention = TimeSpan.FromMinutes(15),
    ResponseFormat = GetResponseFormat()
};
```

- [ ] **Step 5: Trim the system prompt**

Remove the `Respond **only** with valid JSON…` paragraph and the `JSON structure` + `Example` blocks. Keep the editorial-refinement role text.

Likewise, strip the trailing `Return JSON with: translation (string), reasoning (string explaining changes made).` line from the user prompt.

- [ ] **Step 6: Replace the try/catch JSON parsing with the direct deserializer**

Replace this block:

```csharp
JsonElement json;
try
{
    json = JsonSerializer.Deserialize<JsonElement>(
        response.Outputs.First().Choices.First().Message.Content);
}
catch (JsonException ex)
{
    var jsonString = JsonUtils.ParseJsonString(response.Outputs.First().Choices.First().Message.Content);
    json = JsonSerializer.Deserialize<JsonElement>(jsonString);
}
```

with:

```csharp
var json = JsonSerializer.Deserialize<JsonElement>(
    response.Outputs.First().Choices.First().Message.Content);
```

The structured response makes the bracket-matching fallback dead code.

- [ ] **Step 7: Remove the `Console.WriteLine($"LOG RefineTranslationActivity response: ...")` line**

Delete it.

- [ ] **Step 8: Clean unused `using`s**

The file currently has these unused imports — remove them:

```csharp
using Microsoft.VisualBasic;
using System.Runtime.InteropServices;
using System.Xml;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.ComponentModel.DataAnnotations;
```

- [ ] **Step 9: Delete `JsonUtils.cs`**

Run:

```bash
git rm /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems/AlienTranslator/AlienTranslator.ApiService/Activities/JsonUtils.cs
```

- [ ] **Step 10: Build & commit**

```bash
dotnet build /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems/AlienTranslator/AlienTranslator.ApiService/AlienTranslator.ApiService.csproj
cd /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems
git add AlienTranslator/AlienTranslator.ApiService/Activities/RefineTranslationActivity.cs
git commit -m "refactor(AlienTranslator): use ResponseFormat for RefineTranslationActivity; drop JsonUtils fallback"
```

- [ ] **Step 11: Smoke-test the AlienTranslator workflow**

Start the AlienTranslator AppHost (the project's standard Aspire run). Trigger the workflow through `AlienTranslator/AlienTranslator.ApiService/local.http` (or the equivalent). Confirm:

- The workflow completes without `JsonException`.
- The final translation and evaluation result are populated (no empty strings, no zero scores from missing fields).

If parsing fails, the most likely cause is a required field missing from a schema — re-check the property list against the result model record fields.

---

## Task 6: Migrate `GalacticAnomalyClassifier/Activities/ClassifyAnomalyActivity.cs`

**Files:**
- Modify: `GalacticAnomalyClassifier/GalacticAnomalyClassifier.ApiService/Activities/ClassifyAnomalyActivity.cs`

- [ ] **Step 1: Add `using` directive**

Add `using Google.Protobuf.WellKnownTypes;`.

- [ ] **Step 2: Add `GetResponseFormat()`**

```csharp
private static Struct GetResponseFormat()
{
    var stringType = new Struct();
    stringType.Fields.Add("type", Value.ForString("string"));

    var numberType = new Struct();
    numberType.Fields.Add("type", Value.ForString("number"));

    var properties = new Struct();
    properties.Fields.Add("type", Value.ForStruct(stringType));
    properties.Fields.Add("confidence", Value.ForStruct(numberType));
    properties.Fields.Add("reasoning", Value.ForStruct(stringType));

    var responseFormat = new Struct();
    responseFormat.Fields.Add("type", Value.ForString("object"));
    responseFormat.Fields.Add("properties", Value.ForStruct(properties));
    responseFormat.Fields.Add("required", Value.ForList(
        Value.ForString("type"),
        Value.ForString("confidence"),
        Value.ForString("reasoning")));

    return responseFormat;
}
```

- [ ] **Step 3: Wire `ConversationOptions`**

```csharp
var conversationOptions = new ConversationOptions("conversation")
{
    Temperature = 0.7,
    PromptCacheRetention = TimeSpan.FromMinutes(15),
    ResponseFormat = GetResponseFormat()
};
```

- [ ] **Step 4: Trim the system prompt**

Remove the JSON-only boilerplate, `JSON structure` block, and `Example` block. Keep the listing of valid categories (TEMPORAL RIFT, DARK MATTER CLUSTER, ALIEN ARTIFACT, STELLAR PHENOMENON, DIMENSIONAL TEAR) — that's content guidance, not formatting.

- [ ] **Step 5: Remove `Console.WriteLine` debug lines.**

- [ ] **Step 6: Build & commit**

```bash
dotnet build /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems/GalacticAnomalyClassifier/GalacticAnomalyClassifier.ApiService/GalacticAnomalyClassifier.ApiService.csproj
cd /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems
git add GalacticAnomalyClassifier/GalacticAnomalyClassifier.ApiService/Activities/ClassifyAnomalyActivity.cs
git commit -m "refactor(GalacticAnomalyClassifier): use ResponseFormat for ClassifyAnomalyActivity"
```

---

## Task 7: Migrate `GalacticAnomalyClassifier/Activities/AnalyzeStellarPhenomenonActivity.cs`

**Files:**
- Modify: `GalacticAnomalyClassifier/GalacticAnomalyClassifier.ApiService/Activities/AnalyzeStellarPhenomenonActivity.cs`

The result type `StellarAnalysis(string Analysis, Dictionary<string, object> AstrophysicsData, List<string> ObservationProtocols, string RadiationLevel)` includes an open-shape dictionary — handle this with `"type": "object"` and no `properties` (free-form map).

Note: the existing prompt describes `observationProtocols` as `"<list of observation protocols>"` (string) but the activity deserializes it as a `List<string>` via `JsonSerializer.Deserialize<List<string>>(...)`. The schema must declare it as an array; this also fixes a latent bug where the model could return a string instead of an array.

- [ ] **Step 1: Add `using` directive**

Add `using Google.Protobuf.WellKnownTypes;`.

- [ ] **Step 2: Add `GetResponseFormat()`**

```csharp
private static Struct GetResponseFormat()
{
    var stringType = new Struct();
    stringType.Fields.Add("type", Value.ForString("string"));

    var openObjectType = new Struct();
    openObjectType.Fields.Add("type", Value.ForString("object"));

    var stringArrayType = new Struct();
    stringArrayType.Fields.Add("type", Value.ForString("array"));
    stringArrayType.Fields.Add("items", Value.ForStruct(stringType));

    var properties = new Struct();
    properties.Fields.Add("analysis", Value.ForStruct(stringType));
    properties.Fields.Add("astrophysicsData", Value.ForStruct(openObjectType));
    properties.Fields.Add("observationProtocols", Value.ForStruct(stringArrayType));
    properties.Fields.Add("radiationLevel", Value.ForStruct(stringType));

    var responseFormat = new Struct();
    responseFormat.Fields.Add("type", Value.ForString("object"));
    responseFormat.Fields.Add("properties", Value.ForStruct(properties));
    responseFormat.Fields.Add("required", Value.ForList(
        Value.ForString("analysis"),
        Value.ForString("astrophysicsData"),
        Value.ForString("observationProtocols"),
        Value.ForString("radiationLevel")));

    return responseFormat;
}
```

- [ ] **Step 3: Wire `ConversationOptions`**

```csharp
var conversationOptions = new ConversationOptions("conversation")
{
    Temperature = 0.7,
    PromptCacheRetention = TimeSpan.FromMinutes(15),
    ResponseFormat = GetResponseFormat()
};
```

- [ ] **Step 4: Trim system prompt**

Strip the JSON-formatting boilerplate, `JSON structure` block, `Example` block. Keep the astrophysicist role and the bullet list of analysis dimensions (energy output, radiation levels, etc.).

If the prompt currently asks the model to use scientific-E notation, **keep that line** — it's a content hint, not a format hint. (It will appear as string values inside `astrophysicsData`; the schema doesn't constrain dictionary values.)

- [ ] **Step 5: Remove `Console.WriteLine` debug lines.**

- [ ] **Step 6: Build & commit**

```bash
dotnet build /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems/GalacticAnomalyClassifier/GalacticAnomalyClassifier.ApiService/GalacticAnomalyClassifier.ApiService.csproj
cd /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems
git add GalacticAnomalyClassifier/GalacticAnomalyClassifier.ApiService/Activities/AnalyzeStellarPhenomenonActivity.cs
git commit -m "refactor(GalacticAnomalyClassifier): use ResponseFormat for AnalyzeStellarPhenomenonActivity"
```

---

## Task 8: Migrate `GalacticAnomalyClassifier/Activities/AnalyzeTemporalRiftActivity.cs`

**Files:**
- Modify: `GalacticAnomalyClassifier/GalacticAnomalyClassifier.ApiService/Activities/AnalyzeTemporalRiftActivity.cs`

Result type `TemporalAnalysis(string Analysis, Dictionary<string, object> QuantumMetrics, List<string> SafetyProtocols, string TimelineStability)`.

- [ ] **Step 1: Add `using` directive**

Add `using Google.Protobuf.WellKnownTypes;`.

- [ ] **Step 2: Add `GetResponseFormat()`**

```csharp
private static Struct GetResponseFormat()
{
    var stringType = new Struct();
    stringType.Fields.Add("type", Value.ForString("string"));

    var openObjectType = new Struct();
    openObjectType.Fields.Add("type", Value.ForString("object"));

    var stringArrayType = new Struct();
    stringArrayType.Fields.Add("type", Value.ForString("array"));
    stringArrayType.Fields.Add("items", Value.ForStruct(stringType));

    var properties = new Struct();
    properties.Fields.Add("analysis", Value.ForStruct(stringType));
    properties.Fields.Add("quantumMetrics", Value.ForStruct(openObjectType));
    properties.Fields.Add("safetyProtocols", Value.ForStruct(stringArrayType));
    properties.Fields.Add("timelineStability", Value.ForStruct(stringType));

    var responseFormat = new Struct();
    responseFormat.Fields.Add("type", Value.ForString("object"));
    responseFormat.Fields.Add("properties", Value.ForStruct(properties));
    responseFormat.Fields.Add("required", Value.ForList(
        Value.ForString("analysis"),
        Value.ForString("quantumMetrics"),
        Value.ForString("safetyProtocols"),
        Value.ForString("timelineStability")));

    return responseFormat;
}
```

- [ ] **Step 3: Wire `ConversationOptions`**

```csharp
var conversationOptions = new ConversationOptions("conversation")
{
    Temperature = 0.7,
    PromptCacheRetention = TimeSpan.FromMinutes(15),
    ResponseFormat = GetResponseFormat()
};
```

- [ ] **Step 4: Trim system prompt**

Strip the `Respond **only** with valid JSON…` paragraph, the `JSON structure` block, and the `Example` block. Keep the temporal-rift specialist role description and the bullet list of analysis dimensions (timeline stability, energy signatures, paradox risk, etc.). If the prompt mentions valid `timelineStability` values, keep that — it's content guidance.

- [ ] **Step 5: Remove any `Console.WriteLine` debug lines.**

- [ ] **Step 6: Build & commit**

```bash
dotnet build /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems/GalacticAnomalyClassifier/GalacticAnomalyClassifier.ApiService/GalacticAnomalyClassifier.ApiService.csproj
cd /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems
git add GalacticAnomalyClassifier/GalacticAnomalyClassifier.ApiService/Activities/AnalyzeTemporalRiftActivity.cs
git commit -m "refactor(GalacticAnomalyClassifier): use ResponseFormat for AnalyzeTemporalRiftActivity"
```

---

## Task 9: Migrate `GalacticAnomalyClassifier/Activities/AnalyzeAlienArtifactActivity.cs`

**Files:**
- Modify: `GalacticAnomalyClassifier/GalacticAnomalyClassifier.ApiService/Activities/AnalyzeAlienArtifactActivity.cs`

Result type `ArtifactAnalysis(string Analysis, Dictionary<string, object> XenoarchaeologyData, List<string> ExtractionProcedures, string HostilityIndicators)`.

⚠ **Field-name asymmetry:** the prompt currently asks for `hostilityIndicator` (singular) but the record property is `HostilityIndicators` (plural) and `GetProperty("hostilityIndicator")` reads singular. To stay byte-identical to current behavior, use `"hostilityIndicator"` in the schema property name (matching the wire format). Do **not** rename the record property.

- [ ] **Step 1: Add `using` directive**

Add `using Google.Protobuf.WellKnownTypes;`.

- [ ] **Step 2: Add `GetResponseFormat()`**

```csharp
private static Struct GetResponseFormat()
{
    var stringType = new Struct();
    stringType.Fields.Add("type", Value.ForString("string"));

    var openObjectType = new Struct();
    openObjectType.Fields.Add("type", Value.ForString("object"));

    var stringArrayType = new Struct();
    stringArrayType.Fields.Add("type", Value.ForString("array"));
    stringArrayType.Fields.Add("items", Value.ForStruct(stringType));

    var properties = new Struct();
    properties.Fields.Add("analysis", Value.ForStruct(stringType));
    properties.Fields.Add("xenoarchaeologyData", Value.ForStruct(openObjectType));
    properties.Fields.Add("extractionProcedures", Value.ForStruct(stringArrayType));
    properties.Fields.Add("hostilityIndicator", Value.ForStruct(stringType));

    var responseFormat = new Struct();
    responseFormat.Fields.Add("type", Value.ForString("object"));
    responseFormat.Fields.Add("properties", Value.ForStruct(properties));
    responseFormat.Fields.Add("required", Value.ForList(
        Value.ForString("analysis"),
        Value.ForString("xenoarchaeologyData"),
        Value.ForString("extractionProcedures"),
        Value.ForString("hostilityIndicator")));

    return responseFormat;
}
```

- [ ] **Step 3: Wire `ConversationOptions`**

```csharp
var conversationOptions = new ConversationOptions("conversation")
{
    Temperature = 0.7,
    PromptCacheRetention = TimeSpan.FromMinutes(15),
    ResponseFormat = GetResponseFormat()
};
```

- [ ] **Step 4: Trim system prompt**

Strip the `Respond **only** with valid JSON…` paragraph, the `JSON structure` block, and the `Example` block. Keep the xenoarchaeology role description, the analysis-dimension bullets, and any mention of valid `hostilityIndicator` values (SAFE, CAUTION, DANGEROUS, LETHAL).

- [ ] **Step 5: Remove any `Console.WriteLine` debug lines.**

- [ ] **Step 6: Build & commit**

```bash
dotnet build /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems/GalacticAnomalyClassifier/GalacticAnomalyClassifier.ApiService/GalacticAnomalyClassifier.ApiService.csproj
cd /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems
git add GalacticAnomalyClassifier/GalacticAnomalyClassifier.ApiService/Activities/AnalyzeAlienArtifactActivity.cs
git commit -m "refactor(GalacticAnomalyClassifier): use ResponseFormat for AnalyzeAlienArtifactActivity"
```

---

## Task 10: Migrate `GalacticAnomalyClassifier/Activities/AnalyzeDimensionalTearActivity.cs`

**Files:**
- Modify: `GalacticAnomalyClassifier/GalacticAnomalyClassifier.ApiService/Activities/AnalyzeDimensionalTearActivity.cs`

Result type `DimensionalAnalysis(string Analysis, Dictionary<string, object> MultiverseMetrics, List<string> ContainmentProcedures, string RealityStability)`.

The wire field is `spacetimeTearSeverity` (per the current `GetProperty(...)` call), mapped to the C# property `RealityStability`. Use the wire name in the schema.

- [ ] **Step 1: Add `using` directive**

Add `using Google.Protobuf.WellKnownTypes;`.

- [ ] **Step 2: Add `GetResponseFormat()`**

```csharp
private static Struct GetResponseFormat()
{
    var stringType = new Struct();
    stringType.Fields.Add("type", Value.ForString("string"));

    var openObjectType = new Struct();
    openObjectType.Fields.Add("type", Value.ForString("object"));

    var stringArrayType = new Struct();
    stringArrayType.Fields.Add("type", Value.ForString("array"));
    stringArrayType.Fields.Add("items", Value.ForStruct(stringType));

    var properties = new Struct();
    properties.Fields.Add("analysis", Value.ForStruct(stringType));
    properties.Fields.Add("multiverseMetrics", Value.ForStruct(openObjectType));
    properties.Fields.Add("containmentProcedures", Value.ForStruct(stringArrayType));
    properties.Fields.Add("spacetimeTearSeverity", Value.ForStruct(stringType));

    var responseFormat = new Struct();
    responseFormat.Fields.Add("type", Value.ForString("object"));
    responseFormat.Fields.Add("properties", Value.ForStruct(properties));
    responseFormat.Fields.Add("required", Value.ForList(
        Value.ForString("analysis"),
        Value.ForString("multiverseMetrics"),
        Value.ForString("containmentProcedures"),
        Value.ForString("spacetimeTearSeverity")));

    return responseFormat;
}
```

- [ ] **Step 3: Wire `ConversationOptions`**

```csharp
var conversationOptions = new ConversationOptions("conversation")
{
    Temperature = 0.7,
    PromptCacheRetention = TimeSpan.FromMinutes(15),
    ResponseFormat = GetResponseFormat()
};
```

- [ ] **Step 4: Trim system prompt**

Strip the `Respond **only** with valid JSON…` paragraph, the `JSON structure` block, and the `Example` block. Keep the dimensional-physicist role and the bullet list of analysis dimensions (multiverse interactions, containment, reality stability, etc.). If the prompt mentions valid `spacetimeTearSeverity` values (LOW, MEDIUM, HIGH, CRITICAL), keep that line.

- [ ] **Step 5: Remove any `Console.WriteLine` debug lines.**

- [ ] **Step 6: Build & commit**

```bash
dotnet build /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems/GalacticAnomalyClassifier/GalacticAnomalyClassifier.ApiService/GalacticAnomalyClassifier.ApiService.csproj
cd /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems
git add GalacticAnomalyClassifier/GalacticAnomalyClassifier.ApiService/Activities/AnalyzeDimensionalTearActivity.cs
git commit -m "refactor(GalacticAnomalyClassifier): use ResponseFormat for AnalyzeDimensionalTearActivity"
```

---

## Task 11: Migrate `GalacticAnomalyClassifier/Activities/AnalyzeDarkMatterActivity.cs`

**Files:**
- Modify: `GalacticAnomalyClassifier/GalacticAnomalyClassifier.ApiService/Activities/AnalyzeDarkMatterActivity.cs`

Result type `DarkMatterAnalysis(string Analysis, Dictionary<string, object> GravitationalData, List<string> HarvestingOpportunities, string CollapseProbability)`.

- [ ] **Step 1: Add `using` directive**

Add `using Google.Protobuf.WellKnownTypes;`.

- [ ] **Step 2: Add `GetResponseFormat()`**

```csharp
private static Struct GetResponseFormat()
{
    var stringType = new Struct();
    stringType.Fields.Add("type", Value.ForString("string"));

    var openObjectType = new Struct();
    openObjectType.Fields.Add("type", Value.ForString("object"));

    var stringArrayType = new Struct();
    stringArrayType.Fields.Add("type", Value.ForString("array"));
    stringArrayType.Fields.Add("items", Value.ForStruct(stringType));

    var properties = new Struct();
    properties.Fields.Add("analysis", Value.ForStruct(stringType));
    properties.Fields.Add("gravitationalData", Value.ForStruct(openObjectType));
    properties.Fields.Add("harvestingOpportunities", Value.ForStruct(stringArrayType));
    properties.Fields.Add("collapseProbability", Value.ForStruct(stringType));

    var responseFormat = new Struct();
    responseFormat.Fields.Add("type", Value.ForString("object"));
    responseFormat.Fields.Add("properties", Value.ForStruct(properties));
    responseFormat.Fields.Add("required", Value.ForList(
        Value.ForString("analysis"),
        Value.ForString("gravitationalData"),
        Value.ForString("harvestingOpportunities"),
        Value.ForString("collapseProbability")));

    return responseFormat;
}
```

- [ ] **Step 3: Wire `ConversationOptions`**

```csharp
var conversationOptions = new ConversationOptions("conversation")
{
    Temperature = 0.7,
    PromptCacheRetention = TimeSpan.FromMinutes(15),
    ResponseFormat = GetResponseFormat()
};
```

- [ ] **Step 4: Trim system prompt**

Strip the `Respond **only** with valid JSON…` paragraph, the `JSON structure` block, and the `Example` block. Keep the dark-matter specialist role, the analysis-dimension bullets, and any mention of valid `collapseProbability` values (LOW, MEDIUM, HIGH, CRITICAL).

- [ ] **Step 5: Remove any `Console.WriteLine` debug lines.**

- [ ] **Step 6: Build & commit**

```bash
dotnet build /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems/GalacticAnomalyClassifier/GalacticAnomalyClassifier.ApiService/GalacticAnomalyClassifier.ApiService.csproj
cd /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems
git add GalacticAnomalyClassifier/GalacticAnomalyClassifier.ApiService/Activities/AnalyzeDarkMatterActivity.cs
git commit -m "refactor(GalacticAnomalyClassifier): use ResponseFormat for AnalyzeDarkMatterActivity"
```

- [ ] **Step 5: Smoke-test the GalacticAnomalyClassifier workflow**

Trigger the classifier workflow via the project's `local.http`. Send a sample anomaly that exercises each branch (or at least one branch) and confirm the returned analysis populates all four fields.

---

## Task 12: Migrate `SpaceColonyPlanner/Activities/Analysis/AnalyzePlanetActivity.cs`

**Files:**
- Modify: `SpaceColonyPlanner/SpaceColonyPlanner.ApiService/Activities/Analysis/AnalyzePlanetActivity.cs`

Result type `PlanetAnalysis(List<string> Challenges, List<string> Opportunities, string RecommendedApproach)`.

- [ ] **Step 1: Add `using` directive**

Add `using Google.Protobuf.WellKnownTypes;`.

- [ ] **Step 2: Add `GetResponseFormat()`**

```csharp
private static Struct GetResponseFormat()
{
    var stringType = new Struct();
    stringType.Fields.Add("type", Value.ForString("string"));

    var stringArrayType = new Struct();
    stringArrayType.Fields.Add("type", Value.ForString("array"));
    stringArrayType.Fields.Add("items", Value.ForStruct(stringType));

    var properties = new Struct();
    properties.Fields.Add("challenges", Value.ForStruct(stringArrayType));
    properties.Fields.Add("opportunities", Value.ForStruct(stringArrayType));
    properties.Fields.Add("recommendedApproach", Value.ForStruct(stringType));

    var responseFormat = new Struct();
    responseFormat.Fields.Add("type", Value.ForString("object"));
    responseFormat.Fields.Add("properties", Value.ForStruct(properties));
    responseFormat.Fields.Add("required", Value.ForList(
        Value.ForString("challenges"),
        Value.ForString("opportunities"),
        Value.ForString("recommendedApproach")));

    return responseFormat;
}
```

- [ ] **Step 3: Wire `ConversationOptions`**

```csharp
var options = new ConversationOptions("conversation")
{
    Temperature = 0.7,
    PromptCacheRetention = TimeSpan.FromMinutes(15),
    ResponseFormat = GetResponseFormat()
};
```

- [ ] **Step 4: Trim system prompt; remove `Console.WriteLine` lines.**

- [ ] **Step 5: Build & commit**

```bash
dotnet build /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems/SpaceColonyPlanner/SpaceColonyPlanner.ApiService/SpaceColonyPlanner.ApiService.csproj
cd /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems
git add SpaceColonyPlanner/SpaceColonyPlanner.ApiService/Activities/Analysis/AnalyzePlanetActivity.cs
git commit -m "refactor(SpaceColonyPlanner): use ResponseFormat for AnalyzePlanetActivity"
```

---

## Task 13: Migrate `SpaceColonyPlanner/Activities/Analysis/DetermineStructuresActivity.cs`

**Files:**
- Modify: `SpaceColonyPlanner/SpaceColonyPlanner.ApiService/Activities/Analysis/DetermineStructuresActivity.cs`

Result type `List<StructureRequest>` where `StructureRequest(string StructureType, Priority Priority, int Quantity, string Reasoning)` and `Priority` is `enum { Critical, High, Medium, Low }`. The wire format is an envelope `{ "structures": [ {...}, {...} ] }`, not a bare array.

The activity parses `Priority` via `Enum.Parse<Priority>(...)`. The schema's `priority` property remains `"type": "string"` and the model is steered to one of the four values via the existing prompt content.

- [ ] **Step 1: Add `using` directive**

Add `using Google.Protobuf.WellKnownTypes;`.

- [ ] **Step 2: Add `GetResponseFormat()`**

```csharp
private static Struct GetResponseFormat()
{
    var stringType = new Struct();
    stringType.Fields.Add("type", Value.ForString("string"));

    var integerType = new Struct();
    integerType.Fields.Add("type", Value.ForString("integer"));

    // Inner object: one structure request
    var structureProps = new Struct();
    structureProps.Fields.Add("structureType", Value.ForStruct(stringType));
    structureProps.Fields.Add("priority", Value.ForStruct(stringType));
    structureProps.Fields.Add("quantity", Value.ForStruct(integerType));
    structureProps.Fields.Add("reasoning", Value.ForStruct(stringType));

    var structureType = new Struct();
    structureType.Fields.Add("type", Value.ForString("object"));
    structureType.Fields.Add("properties", Value.ForStruct(structureProps));
    structureType.Fields.Add("required", Value.ForList(
        Value.ForString("structureType"),
        Value.ForString("priority"),
        Value.ForString("quantity"),
        Value.ForString("reasoning")));

    // Outer: array of structures
    var structuresArrayType = new Struct();
    structuresArrayType.Fields.Add("type", Value.ForString("array"));
    structuresArrayType.Fields.Add("items", Value.ForStruct(structureType));

    var properties = new Struct();
    properties.Fields.Add("structures", Value.ForStruct(structuresArrayType));

    var responseFormat = new Struct();
    responseFormat.Fields.Add("type", Value.ForString("object"));
    responseFormat.Fields.Add("properties", Value.ForStruct(properties));
    responseFormat.Fields.Add("required", Value.ForList(
        Value.ForString("structures")));

    return responseFormat;
}
```

- [ ] **Step 3: Wire `ConversationOptions`** (with `Temperature = 0.7f`).

- [ ] **Step 4: Trim system prompt.** Keep the listing of valid `structureType` values (HabitatDome, PowerPlant, Agriculture, MiningFacility, ResearchLab, DefenseSystem) and valid `Priority` values (Critical, High, Medium, Low) — those are content guidance the model still needs.

- [ ] **Step 5: Build & commit**

```bash
dotnet build /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems/SpaceColonyPlanner/SpaceColonyPlanner.ApiService/SpaceColonyPlanner.ApiService.csproj
cd /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems
git add SpaceColonyPlanner/SpaceColonyPlanner.ApiService/Activities/Analysis/DetermineStructuresActivity.cs
git commit -m "refactor(SpaceColonyPlanner): use ResponseFormat for DetermineStructuresActivity"
```

---

## Task 14: Migrate `SpaceColonyPlanner/Activities/Analysis/SynthesizePlanActivity.cs`

**Files:**
- Modify: `SpaceColonyPlanner/SpaceColonyPlanner.ApiService/Activities/Analysis/SynthesizePlanActivity.cs`

Result type `ColonyMasterPlan` is built partly from input and partly from LLM output. The LLM output shape is:

```
{
  "timeline": [
    { "phaseNumber": <int>, "name": <string>, "structures": [<string>, ...], "durationDays": <int> },
    ...
  ],
  "successFactors": <string>,
  "riskAssessment": <string>
}
```

- [ ] **Step 1: Add `using` directive**

Add `using Google.Protobuf.WellKnownTypes;`.

- [ ] **Step 2: Add `GetResponseFormat()`**

```csharp
private static Struct GetResponseFormat()
{
    var stringType = new Struct();
    stringType.Fields.Add("type", Value.ForString("string"));

    var integerType = new Struct();
    integerType.Fields.Add("type", Value.ForString("integer"));

    var stringArrayType = new Struct();
    stringArrayType.Fields.Add("type", Value.ForString("array"));
    stringArrayType.Fields.Add("items", Value.ForStruct(stringType));

    // Inner: one phase
    var phaseProps = new Struct();
    phaseProps.Fields.Add("phaseNumber", Value.ForStruct(integerType));
    phaseProps.Fields.Add("name", Value.ForStruct(stringType));
    phaseProps.Fields.Add("structures", Value.ForStruct(stringArrayType));
    phaseProps.Fields.Add("durationDays", Value.ForStruct(integerType));

    var phaseType = new Struct();
    phaseType.Fields.Add("type", Value.ForString("object"));
    phaseType.Fields.Add("properties", Value.ForStruct(phaseProps));
    phaseType.Fields.Add("required", Value.ForList(
        Value.ForString("phaseNumber"),
        Value.ForString("name"),
        Value.ForString("structures"),
        Value.ForString("durationDays")));

    var timelineArrayType = new Struct();
    timelineArrayType.Fields.Add("type", Value.ForString("array"));
    timelineArrayType.Fields.Add("items", Value.ForStruct(phaseType));

    var properties = new Struct();
    properties.Fields.Add("timeline", Value.ForStruct(timelineArrayType));
    properties.Fields.Add("successFactors", Value.ForStruct(stringType));
    properties.Fields.Add("riskAssessment", Value.ForStruct(stringType));

    var responseFormat = new Struct();
    responseFormat.Fields.Add("type", Value.ForString("object"));
    responseFormat.Fields.Add("properties", Value.ForStruct(properties));
    responseFormat.Fields.Add("required", Value.ForList(
        Value.ForString("timeline"),
        Value.ForString("successFactors"),
        Value.ForString("riskAssessment")));

    return responseFormat;
}
```

- [ ] **Step 3: Wire `ConversationOptions`**

```csharp
var options = new ConversationOptions("conversation")
{
    Temperature = 0.7,
    PromptCacheRetention = TimeSpan.FromMinutes(15),
    ResponseFormat = GetResponseFormat()
};
```

- [ ] **Step 4: Trim system prompt**

Strip the `Respond **only** with valid JSON…` paragraph, the `JSON structure` block, and the `Example` block. Keep the master-planner role and the description of what each timeline phase should contain.

- [ ] **Step 5: Remove any `Console.WriteLine` debug lines.**

- [ ] **Step 6: Build & commit**

```bash
dotnet build /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems/SpaceColonyPlanner/SpaceColonyPlanner.ApiService/SpaceColonyPlanner.ApiService.csproj
cd /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems
git add SpaceColonyPlanner/SpaceColonyPlanner.ApiService/Activities/Analysis/SynthesizePlanActivity.cs
git commit -m "refactor(SpaceColonyPlanner): use ResponseFormat for SynthesizePlanActivity"
```

---

## Task 15: Create shared `StructurePlanSchema` for SpaceColonyPlanner workers

The 7 worker activities under `SpaceColonyPlanner.ApiService/Activities/Workers/` all return `StructurePlan` and currently use an identical inline `ParseStructurePlan` helper. Their schemas would be byte-identical — extract once.

**Files:**
- Create: `SpaceColonyPlanner/SpaceColonyPlanner.ApiService/Activities/Workers/StructurePlanSchema.cs`

- [ ] **Step 1: Create the file with contents**

```csharp
using Google.Protobuf.WellKnownTypes;

namespace SpaceColonyPlanner.Activities.Workers;

internal static class StructurePlanSchema
{
    public static Struct Get()
    {
        var stringType = new Struct();
        stringType.Fields.Add("type", Value.ForString("string"));

        var integerType = new Struct();
        integerType.Fields.Add("type", Value.ForString("integer"));

        var stringArrayType = new Struct();
        stringArrayType.Fields.Add("type", Value.ForString("array"));
        stringArrayType.Fields.Add("items", Value.ForStruct(stringType));

        var properties = new Struct();
        properties.Fields.Add("structureType", Value.ForStruct(stringType));
        properties.Fields.Add("quantity", Value.ForStruct(integerType));
        properties.Fields.Add("materials", Value.ForStruct(stringArrayType));
        properties.Fields.Add("constructionDays", Value.ForStruct(integerType));
        properties.Fields.Add("workerHours", Value.ForStruct(integerType));
        properties.Fields.Add("prerequisites", Value.ForStruct(stringArrayType));
        properties.Fields.Add("detailedSpecification", Value.ForStruct(stringType));

        var responseFormat = new Struct();
        responseFormat.Fields.Add("type", Value.ForString("object"));
        responseFormat.Fields.Add("properties", Value.ForStruct(properties));
        responseFormat.Fields.Add("required", Value.ForList(
            Value.ForString("structureType"),
            Value.ForString("quantity"),
            Value.ForString("materials"),
            Value.ForString("constructionDays"),
            Value.ForString("workerHours"),
            Value.ForString("prerequisites"),
            Value.ForString("detailedSpecification")));

        return responseFormat;
    }
}
```

- [ ] **Step 2: Build**

```bash
dotnet build /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems/SpaceColonyPlanner/SpaceColonyPlanner.ApiService/SpaceColonyPlanner.ApiService.csproj
```

Expected: `Build succeeded`. The new file is unused at this point — it builds anyway.

- [ ] **Step 3: Commit**

```bash
cd /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems
git add SpaceColonyPlanner/SpaceColonyPlanner.ApiService/Activities/Workers/StructurePlanSchema.cs
git commit -m "feat(SpaceColonyPlanner): add shared StructurePlanSchema for worker activities"
```

---

## Task 16: Migrate all 7 SpaceColonyPlanner worker activities

**Files (all modified):**
- `SpaceColonyPlanner/SpaceColonyPlanner.ApiService/Activities/Workers/PlanPowerPlantActivity.cs`
- `SpaceColonyPlanner/SpaceColonyPlanner.ApiService/Activities/Workers/PlanHabitatDomeActivity.cs`
- `SpaceColonyPlanner/SpaceColonyPlanner.ApiService/Activities/Workers/PlanAgricultureActivity.cs`
- `SpaceColonyPlanner/SpaceColonyPlanner.ApiService/Activities/Workers/PlanMiningFacilityActivity.cs`
- `SpaceColonyPlanner/SpaceColonyPlanner.ApiService/Activities/Workers/PlanResearchLabActivity.cs`
- `SpaceColonyPlanner/SpaceColonyPlanner.ApiService/Activities/Workers/PlanDefenseSystemActivity.cs`
- `SpaceColonyPlanner/SpaceColonyPlanner.ApiService/Activities/Workers/UnknownStructureActivity.cs`

Apply the same edits to each file:

- [ ] **Step 1: For each file, in `RunAsync`, replace the `ConversationOptions` block with**

```csharp
var options = new ConversationOptions("conversation")
{
    Temperature = 0.7f,
    PromptCacheRetention = TimeSpan.FromMinutes(15),
    ResponseFormat = StructurePlanSchema.Get()
};
```

- [ ] **Step 2: Trim each file's system prompt.** Remove the `Respond **only** with valid JSON …` paragraph and the `JSON structure` + `Example` blocks. Keep the role description and any domain-specific guidance (the power-plant file lists Solar / Nuclear / Geothermal / Fusion options; the habitat-dome file lists its options; etc. — those stay).

- [ ] **Step 3: Remove any `Console.WriteLine` debug logs from each file.**

- [ ] **Step 4: Build**

```bash
dotnet build /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems/SpaceColonyPlanner/SpaceColonyPlanner.ApiService/SpaceColonyPlanner.ApiService.csproj
```

Expected: `Build succeeded`.

- [ ] **Step 5: Commit**

```bash
cd /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems
git add SpaceColonyPlanner/SpaceColonyPlanner.ApiService/Activities/Workers/
git commit -m "refactor(SpaceColonyPlanner): use shared ResponseFormat across worker activities"
```

- [ ] **Step 6: Smoke-test the SpaceColonyPlanner workflow**

Trigger the orchestrator workflow via `local.http`. Confirm every worker branch fires and the resulting `ColonyMasterPlan` populates structures, timeline, materials, success factors, and risk assessment.

---

## Task 17: Migrate `SpaceDebrisAgent/Activities/Agent/AgentReasoningActivity.cs`

**Files:**
- Modify: `SpaceDebrisAgent/SpaceDebrisAgent.ApiService/Activities/Agent/AgentReasoningActivity.cs`

Result type `AgentDecision(int StepNumber, string Reasoning, string ChosenAction, Dictionary<string, object> ActionParameters, string ExpectedOutcome, DateTime Timestamp)`. The `actionParameters` field is open-shape (the agent picks parameters per-tool).

- [ ] **Step 1: Add `using` directive**

Add `using Google.Protobuf.WellKnownTypes;`.

- [ ] **Step 2: Add `GetResponseFormat()`**

```csharp
private static Struct GetResponseFormat()
{
    var stringType = new Struct();
    stringType.Fields.Add("type", Value.ForString("string"));

    var openObjectType = new Struct();
    openObjectType.Fields.Add("type", Value.ForString("object"));

    var properties = new Struct();
    properties.Fields.Add("reasoning", Value.ForStruct(stringType));
    properties.Fields.Add("chosenAction", Value.ForStruct(stringType));
    properties.Fields.Add("actionParameters", Value.ForStruct(openObjectType));
    properties.Fields.Add("expectedOutcome", Value.ForStruct(stringType));

    var responseFormat = new Struct();
    responseFormat.Fields.Add("type", Value.ForString("object"));
    responseFormat.Fields.Add("properties", Value.ForStruct(properties));
    responseFormat.Fields.Add("required", Value.ForList(
        Value.ForString("reasoning"),
        Value.ForString("chosenAction"),
        Value.ForString("actionParameters"),
        Value.ForString("expectedOutcome")));

    return responseFormat;
}
```

- [ ] **Step 3: Wire `ConversationOptions`**

```csharp
var options = new ConversationOptions("conversation")
{
    Temperature = 0.7,
    PromptCacheRetention = TimeSpan.FromMinutes(15),
    ResponseFormat = GetResponseFormat()
};
```

- [ ] **Step 4: Trim system prompt.** Keep the agent role, available-tools list, and the mission-context formatting — that's all content. Remove only the JSON-shape boilerplate.

- [ ] **Step 5: Remove any `Console.WriteLine` debug lines.**

- [ ] **Step 6: Build & commit**

```bash
dotnet build /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems/SpaceDebrisAgent/SpaceDebrisAgent.ApiService/SpaceDebrisAgent.ApiService.csproj
cd /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems
git add SpaceDebrisAgent/SpaceDebrisAgent.ApiService/Activities/Agent/AgentReasoningActivity.cs
git commit -m "refactor(SpaceDebrisAgent): use ResponseFormat for AgentReasoningActivity"
```

---

## Task 18: Migrate `SpaceDebrisAgent/Activities/Tools/ScanDebrisFieldActivity.cs`

**Files:**
- Modify: `SpaceDebrisAgent/SpaceDebrisAgent.ApiService/Activities/Tools/ScanDebrisFieldActivity.cs`

Result type `DebrisField(List<DebrisObject> Debris, double TotalMass, string RiskLevel)` with nested `DebrisObject(string Id, double Mass, string Type, double[] Position, double[] Velocity, string ThreatLevel, bool IsFragmented)`.

- [ ] **Step 1: Add `using` directive**

Add `using Google.Protobuf.WellKnownTypes;`.

- [ ] **Step 2: Add `GetResponseFormat()`**

```csharp
private static Struct GetResponseFormat()
{
    var stringType = new Struct();
    stringType.Fields.Add("type", Value.ForString("string"));

    var numberType = new Struct();
    numberType.Fields.Add("type", Value.ForString("number"));

    var booleanType = new Struct();
    booleanType.Fields.Add("type", Value.ForString("boolean"));

    var numberArrayType = new Struct();
    numberArrayType.Fields.Add("type", Value.ForString("array"));
    numberArrayType.Fields.Add("items", Value.ForStruct(numberType));

    // Inner: one DebrisObject
    var debrisProps = new Struct();
    debrisProps.Fields.Add("id", Value.ForStruct(stringType));
    debrisProps.Fields.Add("mass", Value.ForStruct(numberType));
    debrisProps.Fields.Add("type", Value.ForStruct(stringType));
    debrisProps.Fields.Add("position", Value.ForStruct(numberArrayType));
    debrisProps.Fields.Add("velocity", Value.ForStruct(numberArrayType));
    debrisProps.Fields.Add("threatLevel", Value.ForStruct(stringType));
    debrisProps.Fields.Add("isFragmented", Value.ForStruct(booleanType));

    var debrisItemType = new Struct();
    debrisItemType.Fields.Add("type", Value.ForString("object"));
    debrisItemType.Fields.Add("properties", Value.ForStruct(debrisProps));
    debrisItemType.Fields.Add("required", Value.ForList(
        Value.ForString("id"),
        Value.ForString("mass"),
        Value.ForString("type"),
        Value.ForString("position"),
        Value.ForString("velocity"),
        Value.ForString("threatLevel"),
        Value.ForString("isFragmented")));

    var debrisArrayType = new Struct();
    debrisArrayType.Fields.Add("type", Value.ForString("array"));
    debrisArrayType.Fields.Add("items", Value.ForStruct(debrisItemType));

    var properties = new Struct();
    properties.Fields.Add("debris", Value.ForStruct(debrisArrayType));
    properties.Fields.Add("totalMass", Value.ForStruct(numberType));
    properties.Fields.Add("riskLevel", Value.ForStruct(stringType));

    var responseFormat = new Struct();
    responseFormat.Fields.Add("type", Value.ForString("object"));
    responseFormat.Fields.Add("properties", Value.ForStruct(properties));
    responseFormat.Fields.Add("required", Value.ForList(
        Value.ForString("debris"),
        Value.ForString("totalMass"),
        Value.ForString("riskLevel")));

    return responseFormat;
}
```

- [ ] **Step 3: Wire `ConversationOptions`**

```csharp
var options = new ConversationOptions("conversation")
{
    Temperature = 0.7,
    PromptCacheRetention = TimeSpan.FromMinutes(15),
    ResponseFormat = GetResponseFormat()
};
```

- [ ] **Step 4: Trim system prompt**

Strip the `Respond **only** with valid JSON…` paragraph, the `JSON structure` block, and the `Example` block. Keep the debris-scanner role, the description of what each `DebrisObject` represents, and any mention of valid `threatLevel`/`riskLevel`/`type` enum-like values.

- [ ] **Step 5: Remove any `Console.WriteLine` debug lines.**

- [ ] **Step 6: Build & commit**

```bash
dotnet build /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems/SpaceDebrisAgent/SpaceDebrisAgent.ApiService/SpaceDebrisAgent.ApiService.csproj
cd /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems
git add SpaceDebrisAgent/SpaceDebrisAgent.ApiService/Activities/Tools/ScanDebrisFieldActivity.cs
git commit -m "refactor(SpaceDebrisAgent): use ResponseFormat for ScanDebrisFieldActivity"
```

- [ ] **Step 5: Smoke-test the SpaceDebrisAgent workflow**

Trigger the agent workflow via `local.http`. Confirm at least one `scanDebrisField` tool call returns a populated `DebrisField` and that the agent's reasoning step produces a valid `AgentDecision` with non-empty `actionParameters`.

---

## Task 19: Create shared `ScanResultSchema` for StarshipDiagnostics scanners

The 5 scanner activities all return `ScanResult` and share an identical `ParseScanResult`. Extract the schema once.

**Files:**
- Create: `StarshipDiagnostics/StarshipDiagnostics.ApiService/Activities/Scanners/ScanResultSchema.cs`

- [ ] **Step 1: Create the file**

```csharp
using Google.Protobuf.WellKnownTypes;

namespace StarshipDiagnostics.Activities.Scanners;

internal static class ScanResultSchema
{
    public static Struct Get()
    {
        var stringType = new Struct();
        stringType.Fields.Add("type", Value.ForString("string"));

        var numberType = new Struct();
        numberType.Fields.Add("type", Value.ForString("number"));

        var stringArrayType = new Struct();
        stringArrayType.Fields.Add("type", Value.ForString("array"));
        stringArrayType.Fields.Add("items", Value.ForStruct(stringType));

        var properties = new Struct();
        properties.Fields.Add("status", Value.ForStruct(stringType));
        properties.Fields.Add("healthPercentage", Value.ForStruct(numberType));
        properties.Fields.Add("issues", Value.ForStruct(stringArrayType));
        properties.Fields.Add("recommendations", Value.ForStruct(stringArrayType));
        properties.Fields.Add("detailedAnalysis", Value.ForStruct(stringType));

        var responseFormat = new Struct();
        responseFormat.Fields.Add("type", Value.ForString("object"));
        responseFormat.Fields.Add("properties", Value.ForStruct(properties));
        responseFormat.Fields.Add("required", Value.ForList(
            Value.ForString("status"),
            Value.ForString("healthPercentage"),
            Value.ForString("issues"),
            Value.ForString("recommendations"),
            Value.ForString("detailedAnalysis")));

        return responseFormat;
    }
}
```

- [ ] **Step 2: Build**

```bash
dotnet build /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems/StarshipDiagnostics/StarshipDiagnostics.ApiService/StarshipDiagnostics.ApiService.csproj
```

Expected: `Build succeeded`.

- [ ] **Step 3: Commit**

```bash
cd /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems
git add StarshipDiagnostics/StarshipDiagnostics.ApiService/Activities/Scanners/ScanResultSchema.cs
git commit -m "feat(StarshipDiagnostics): add shared ScanResultSchema for scanner activities"
```

---

## Task 20: Migrate all 5 StarshipDiagnostics scanner activities

**Files (all modified):**
- `StarshipDiagnostics/StarshipDiagnostics.ApiService/Activities/Scanners/HullIntegrityScanActivity.cs`
- `StarshipDiagnostics/StarshipDiagnostics.ApiService/Activities/Scanners/ReactorCoreScanActivity.cs`
- `StarshipDiagnostics/StarshipDiagnostics.ApiService/Activities/Scanners/NavigationScanActivity.cs`
- `StarshipDiagnostics/StarshipDiagnostics.ApiService/Activities/Scanners/WeaponsScanActivity.cs`
- `StarshipDiagnostics/StarshipDiagnostics.ApiService/Activities/Scanners/LifeSupportScanActivity.cs`

Apply the same edits to each:

- [ ] **Step 1: For each file, replace the `ConversationOptions` block in `RunAsync` with**

```csharp
var conversationOptions = new ConversationOptions("conversation")
{
    Temperature = 0.7,
    PromptCacheRetention = TimeSpan.FromMinutes(15),
    ResponseFormat = ScanResultSchema.Get()
};
```

- [ ] **Step 2: Trim each file's system prompt.** Remove the `Respond **only** with valid JSON…` paragraph and the `JSON structure` + `Example` blocks. Keep the role description and the scanner-specific bullet list (e.g., "Micrometeorite impacts / Stress fractures / Corrosion / Structural weak points" for the hull scanner).

- [ ] **Step 3: Remove any `Console.WriteLine` debug logs.**

- [ ] **Step 4: Build**

```bash
dotnet build /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems/StarshipDiagnostics/StarshipDiagnostics.ApiService/StarshipDiagnostics.ApiService.csproj
```

Expected: `Build succeeded`.

- [ ] **Step 5: Commit**

```bash
cd /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems
git add StarshipDiagnostics/StarshipDiagnostics.ApiService/Activities/Scanners/
git commit -m "refactor(StarshipDiagnostics): use shared ResponseFormat across scanner activities"
```

- [ ] **Step 6: Smoke-test the StarshipDiagnostics workflow**

Trigger the workflow via `local.http`. Confirm all five scanner branches return populated `ScanResult` records (subsystem name, status string, health double, issue+recommendation lists, detailed-analysis string).

---

## Task 21: Thread `ShipId` through `AggregateResultsActivity` (StarshipDiagnostics follow-up)

Surfaced while exercising the post-ResponseFormat StarshipDiagnostics workflow: `AggregateResultsActivity` emitted a hardcoded `"SHIP-ID"` literal into the final `DiagnosticReport` instead of the actual ship id from the workflow input. Fix by extending `AggregateResultsInput` with `ShipId`, passing it through from the workflow, and using it in the activity. Unrelated to ResponseFormat but recorded here because it was found and fixed in the same pass.

**Files:**
- Modify: `StarshipDiagnostics/StarshipDiagnostics.ApiService/Models/Starship.cs`
- Modify: `StarshipDiagnostics/StarshipDiagnostics.ApiService/Workflows/ParallelDiagnosticsWorkflow.cs`
- Modify: `StarshipDiagnostics/StarshipDiagnostics.ApiService/Activities/AggregateResultsActivity.cs`

- [ ] **Step 1: Add `ShipId` to `AggregateResultsInput`**

In `Models/Starship.cs`, change the record to:

```csharp
public record AggregateResultsInput(
    string ShipId,
    List<ScanResult> ScanResults,
    List<VoteResult> VoteResults
);
```

- [ ] **Step 2: Pass `input.ShipId` from the workflow**

In `Workflows/ParallelDiagnosticsWorkflow.cs`, update the `AggregateResultsInput` construction:

```csharp
var aggregationInput = new AggregateResultsInput(input.ShipId, scanResults.ToList(), voteResults);
```

- [ ] **Step 3: Use `input.ShipId` in the activity**

In `Activities/AggregateResultsActivity.cs`, replace the hardcoded `"SHIP-ID"` in the `DiagnosticReport` constructor with `input.ShipId`.

- [ ] **Step 4: Build & commit**

```bash
dotnet build /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems/StarshipDiagnostics/StarshipDiagnostics.ApiService/StarshipDiagnostics.ApiService.csproj
cd /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems
git add StarshipDiagnostics/StarshipDiagnostics.ApiService/Models/Starship.cs \
        StarshipDiagnostics/StarshipDiagnostics.ApiService/Workflows/ParallelDiagnosticsWorkflow.cs \
        StarshipDiagnostics/StarshipDiagnostics.ApiService/Activities/AggregateResultsActivity.cs
git commit -m "fix(StarshipDiagnostics): use actual ShipId in DiagnosticReport instead of hardcoded literal"
```

- [ ] **Step 5: Smoke-test**

Re-run the StarshipDiagnostics workflow and confirm the `ShipId` field on the returned `DiagnosticReport` matches the `ShipId` of the input `Starship`.

---

## Task 22: Cross-project final verification

- [ ] **Step 1: Build everything**

```bash
cd /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems
dotnet build AlienTranslator/AlienTranslator.ApiService/AlienTranslator.ApiService.csproj
dotnet build GalacticAnomalyClassifier/GalacticAnomalyClassifier.ApiService/GalacticAnomalyClassifier.ApiService.csproj
dotnet build SpaceColonyPlanner/SpaceColonyPlanner.ApiService/SpaceColonyPlanner.ApiService.csproj
dotnet build SpaceDebrisAgent/SpaceDebrisAgent.ApiService/SpaceDebrisAgent.ApiService.csproj
dotnet build StarshipDiagnostics/StarshipDiagnostics.ApiService/StarshipDiagnostics.ApiService.csproj
```

Expected: all five report `Build succeeded`.

- [ ] **Step 2: Confirm no `Respond **only** with valid JSON` strings remain in scope**

```bash
grep -rn "Respond \*\*only\*\* with valid JSON" \
  /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems/AlienTranslator \
  /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems/GalacticAnomalyClassifier \
  /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems/SpaceColonyPlanner \
  /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems/SpaceDebrisAgent \
  /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems/StarshipDiagnostics
```

Expected: no matches.

- [ ] **Step 3: Confirm every migrated activity references `ResponseFormat`**

```bash
grep -rln "ResponseFormat" \
  /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems/AlienTranslator/AlienTranslator.ApiService/Activities/ \
  /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems/GalacticAnomalyClassifier/GalacticAnomalyClassifier.ApiService/Activities/ \
  /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems/SpaceColonyPlanner/SpaceColonyPlanner.ApiService/Activities/ \
  /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems/SpaceDebrisAgent/SpaceDebrisAgent.ApiService/Activities/ \
  /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems/StarshipDiagnostics/StarshipDiagnostics.ApiService/Activities/
```

Expected: 26 activity files plus the two new `*Schema.cs` files appear.

- [ ] **Step 4: Confirm `JsonUtils` is gone**

```bash
grep -rn "JsonUtils" /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems/AlienTranslator/
```

Expected: no matches.

- [ ] **Step 5: Final tag commit (optional, only if all smoke tests pass)**

If all four per-project smoke tests (Tasks 5, 11, 16, 18, 20) have passed and the Task 21 follow-up has been verified, this work is complete. Otherwise, fix the remaining failures before declaring done.

---

## Out-of-scope notes (do not touch in this plan)

- `AnomalyAnalysis` — no activities request JSON; no migration needed.
- The `ConversationTests` directory at the repo root — these aren't activities in any of the six projects. Out of scope.
- Demo scripts under `.demo/` — they reference the projects but not the activity internals. They should continue to work without modification.
- Workflow orchestration code (`*Workflow.cs` files) — they consume the activity results, not the JSON; signatures are unchanged so they need no edits.
