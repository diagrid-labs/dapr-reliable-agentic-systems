using Dapr.AI.Conversation;
using Dapr.AI.Conversation.ConversationRoles;
using Dapr.AI.Conversation.Extensions;
using Dapr.Workflow;
using Google.Protobuf.WellKnownTypes;
using SpaceDebrisAgent.Models;
using System.Text.Json;

namespace SpaceDebrisAgent.Activities.Agent;

public class AgentReasoningActivity : WorkflowActivity<ReasoningInput, AgentDecision>
{
    private readonly DaprConversationClient _conversationClient;
    
    public AgentReasoningActivity(DaprConversationClient conversationClient)
    {
        _conversationClient = conversationClient;
    }
    
    public override async Task<AgentDecision> RunAsync(
        WorkflowActivityContext context, 
        ReasoningInput input)
    {
        var systemPrompt = @"You are an autonomous space debris cleanup agent. You control a 
spacecraft and must plan and execute debris removal missions.

AVAILABLE TOOLS:
1. SCAN_DEBRIS_FIELD - Scan the area to detect debris objects
2. ANALYZE_DEBRIS {debrisId} - Get detailed info on specific debris
3. MOVE_TO_LOCATION {x, y, z} - Navigate to coordinates
4. CHECK_FUEL - Check remaining fuel
5. CAPTURE_DEBRIS {debrisId} - Attempt to capture debris
6. REQUEST_HUMAN_APPROVAL {reason} - Ask human operator for approval
7. COMPLETE_MISSION - End mission successfully

DECISION-MAKING PROCESS:
- Assess current situation and mission progress
- Consider fuel constraints and efficiency
- Plan logical sequence of actions
- Handle errors and adapt plans
- Request human approval for high-risk actions
- Complete mission when objectives met

JSON structure that describes the fields:
{
  ""reasoning"": ""<string: your analytical thought process for choosing this action>"",
  ""chosenAction"": ""<string: TOOL_NAME from available tools list>"",
  ""actionParameters"": ""<JSON-encoded string of an object with tool-specific parameters>"",
  ""expectedOutcome"": ""<string: what you expect this action to accomplish>""
}";

        var previousActions = string.Join("\n", 
            input.CurrentState.DecisionHistory.TakeLast(5));
        
        var recentToolResults = input.PreviousToolCalls
            .TakeLast(3)
            .Select(tc => $"- {tc.ToolName}: {(tc.Success ? "SUCCESS" : $"FAILED - {tc.ErrorMessage}")}")
            .ToList();
        
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

RECENT TOOL RESULTS:
{string.Join("\n", recentToolResults)}

What is your next action?";

        var options = new ConversationOptions("conversation")
        {
            Temperature = 0.7,
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
                        Name = "AutonomousAgent",
                        Content = [new MessageContent(userPrompt)]
                    }
                })
            ],
            options);
        
        var json = JsonSerializer.Deserialize<JsonElement>(
            response.Outputs.First().Choices.First().Message.Content);
        
        return new AgentDecision(
            input.CurrentState.StepCount,
            json.GetProperty("reasoning").GetString()!,
            json.GetProperty("chosenAction").GetString()!,
            JsonSerializer.Deserialize<Dictionary<string, object>>(
                json.GetProperty("actionParameters").GetString()!)!,
            json.GetProperty("expectedOutcome").GetString()!,
            DateTime.UtcNow
        );
    }

    private static Struct GetResponseFormat()
    {
        var stringType = new Struct();
        stringType.Fields.Add("type", Value.ForString("string"));

        var properties = new Struct();
        properties.Fields.Add("reasoning", Value.ForStruct(stringType));
        properties.Fields.Add("chosenAction", Value.ForStruct(stringType));
        properties.Fields.Add("actionParameters", Value.ForStruct(stringType));
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
}
