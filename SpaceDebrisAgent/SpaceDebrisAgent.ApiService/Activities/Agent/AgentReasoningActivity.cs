using Dapr.AI.Conversation;
using Dapr.AI.Conversation.ConversationRoles;
using Dapr.AI.Conversation.Extensions;
using Dapr.AI.Conversation.Tools;
using Dapr.Workflow;
using SpaceDebrisAgent.Models;
using System.Text.Json;

namespace SpaceDebrisAgent.Activities.Agent;

public class AgentReasoningActivity : WorkflowActivity<ReasoningInput, Metered<AgentDecision>>
{
    private const int MAX_HISTORY = 4; // Tool call exchanges sent back to the LLM

    private readonly DaprConversationClient _conversationClient;

    public AgentReasoningActivity(DaprConversationClient conversationClient)
    {
        _conversationClient = conversationClient;
    }

    public override async Task<Metered<AgentDecision>> RunAsync(
        WorkflowActivityContext context,
        ReasoningInput input)
    {
        var systemPrompt = @"You are an autonomous space debris cleanup agent. You control a
spacecraft and must plan and execute debris removal missions using the available tools.

DECISION-MAKING PROCESS:
- Assess current situation and mission progress
- Consider fuel constraints and efficiency
- Plan logical sequence of actions
- Handle errors and adapt plans
- Request human approval for high-risk actions
- Complete mission when objectives met

Call exactly one tool per turn. Keep `reasoning` <=60 words and `expectedOutcome` <=30 words.";

        var previousActions = string.Join("\n",
            input.CurrentState.DecisionHistory.TakeLast(5));

        var userPrompt = $@"MISSION STATUS:
Step: {input.CurrentState.StepCount}
Phase: {input.CurrentState.CurrentPhase}
Position: [{string.Join(", ", input.CurrentState.Position)}]
Fuel Remaining: {input.CurrentState.FuelRemaining:F2} kg
Debris Captured: {input.CurrentState.CapturedDebris.Count}

MISSION PARAMETERS:
Zone: {input.Mission.OrbitalZone}
Target Debris: {input.Mission.MaxDebrisPieces}
Fuel Budget: {input.Mission.FuelBudget} kg
Max Hours: {input.Mission.MaxMissionHours}
Requires Approval: {input.Mission.RequireHumanApproval}

RECENT ACTIONS:
{previousActions}

What is your next action?";

        var messages = new List<IConversationMessage>
        {
            new SystemMessage { Content = [new MessageContent(systemPrompt)] }
        };
        messages.AddRange(GetToolHistory(input));
        messages.Add(new UserMessage
        {
            Name = "AutonomousAgent",
            Content = [new MessageContent(userPrompt)]
        });

        var options = new ConversationOptions("conversation")
        {
            Temperature = 0.7,
            Tools = GetTools(),
            ToolChoice = ToolChoice.Required
        };

        var response = await _conversationClient.ConverseAsync(
            [new ConversationInput(messages)],
            options);

        // Throwing lets the workflow retry policy ask the LLM again
        var call = response.Outputs.First().Choices.First().Message.ToolCalls
            .OfType<CalledToolFunction>()
            .FirstOrDefault() ?? throw new InvalidOperationException("The LLM did not call a tool.");

        var arguments = JsonSerializer.Deserialize<Dictionary<string, object>>(call.JsonArguments) ?? [];
        var reasoning = arguments.Remove("reasoning", out var r) ? r.ToString()! : "";
        var expectedOutcome = arguments.Remove("expectedOutcome", out var e) ? e.ToString()! : "";

        return new Metered<AgentDecision>(
            new AgentDecision(
                input.CurrentState.StepCount,
                reasoning,
                call.Name,
                arguments,
                expectedOutcome,
                DateTime.UtcNow,
                call.Id
            ),
            response.ToUsage());
    }

    // Each decision is paired with the tool call it produced, so the LLM sees its own
    // tool call followed by the tool result.
    private static IEnumerable<IConversationMessage> GetToolHistory(ReasoningInput input)
    {
        var exchanges = input.PreviousDecisions
            .Zip(input.PreviousToolCalls)
            .TakeLast(MAX_HISTORY);

        foreach (var (decision, toolCall) in exchanges)
        {
            var id = decision.ToolCallId ?? $"call_{decision.StepNumber}";
            var arguments = new Dictionary<string, object>(decision.ActionParameters)
            {
                ["reasoning"] = decision.Reasoning,
                ["expectedOutcome"] = decision.ExpectedOutcome
            };
            var result = toolCall.Success
                ? JsonSerializer.Serialize(toolCall.Result)
                : $"FAILED - {toolCall.ErrorMessage}";

            yield return new AssistantMessage
            {
                ToolCalls = [new CalledToolFunction(decision.ChosenAction, JsonSerializer.Serialize(arguments)) { Id = id }]
            };
            yield return new ToolMessage
            {
                Id = id,
                Name = decision.ChosenAction,
                Content = [new MessageContent(result)]
            };
        }
    }

    private static List<ITool> GetTools() =>
    [
        Tool("SCAN_DEBRIS_FIELD", "Scan the area to detect debris objects."),
        Tool("ANALYZE_DEBRIS", "Get detailed info on specific debris.",
            ("debrisId", "string", "Id of the debris object")),
        Tool("MOVE_TO_LOCATION", "Navigate to coordinates (km).",
            ("x", "number", "Target x coordinate"),
            ("y", "number", "Target y coordinate"),
            ("z", "number", "Target z coordinate")),
        Tool("CHECK_FUEL", "Check remaining fuel."),
        Tool("CAPTURE_DEBRIS", "Attempt to capture debris.",
            ("debrisId", "string", "Id of the debris object")),
        Tool("REQUEST_HUMAN_APPROVAL", "Ask the human operator for approval.",
            ("reason", "string", "Why approval is needed")),
        Tool("COMPLETE_MISSION", "End the mission successfully.")
    ];

    // Every tool also takes `reasoning` and `expectedOutcome`, so the decision can be shown in the UI.
    private static ToolFunction Tool(string name, string description,
        params (string Name, string Type, string Description)[] parameters)
    {
        var all = parameters
            .Append((Name: "reasoning", Type: "string", Description: "Your analytical thought process for choosing this action"))
            .Append((Name: "expectedOutcome", Type: "string", Description: "What you expect this action to accomplish"))
            .ToList();

        return new ToolFunction(name)
        {
            Description = description,
            Parameters = new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = all.ToDictionary(
                    p => p.Name,
                    object? (p) => new Dictionary<string, object?> { ["type"] = p.Type, ["description"] = p.Description }),
                ["required"] = all.Select(p => p.Name).ToList()
            }
        };
    }
}
