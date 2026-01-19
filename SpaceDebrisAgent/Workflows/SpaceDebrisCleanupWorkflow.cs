using Dapr.Workflow;
using SpaceDebrisAgent.Models;
using SpaceDebrisAgent.Activities.Agent;
using SpaceDebrisAgent.Activities.Tools;

namespace SpaceDebrisAgent.Workflows;

public class SpaceDebrisCleanupWorkflow : Workflow<SpaceDebrisCleanupWorkflowInput, MissionResult>
{
    private const int MAX_STEPS = 50; // Prevent infinite loops
    
    private static WorkflowTaskOptions GetDefaultRetryPolicy()
    {
        return new WorkflowTaskOptions(
            new WorkflowRetryPolicy(
                maxNumberOfAttempts: 5,
                firstRetryInterval: TimeSpan.FromSeconds(1)));
    }
    
    public override async Task<MissionResult> RunAsync(
        WorkflowContext context, 
        SpaceDebrisCleanupWorkflowInput input)
    {
        // Get or initialize agent state
        var agentState = input.AgentState ?? new AgentState(
            CurrentPhase: MissionPhase.Planning,
            Position: new[] { 0.0, 0.0, 0.0 },
            FuelRemaining: input.MissionParameters.FuelBudget,
            CapturedDebris: new List<string>(),
            DecisionHistory: new List<string>(),
            StepCount: input.MissionParameters.StepNumber,
            Memory: new Dictionary<string, object>()
        );
        
        agentState = agentState with { StepCount = input.MissionParameters.StepNumber };
        context.SetCustomStatus($"Step: {input.MissionParameters.StepNumber}/{MAX_STEPS}");
        
        // Agent reasoning: decide next action
        var decision = await context.CallActivityAsync<AgentDecision>(
            nameof(AgentReasoningActivity),
            new ReasoningInput(input.MissionParameters, agentState, input.ToolCalls),
            GetDefaultRetryPolicy());
        
        var decisions = input.Decisions.Append(decision).ToList();
        
        // Check if agent decided mission is complete
        if (decision.ChosenAction == "COMPLETE_MISSION")
        {
            var finalReport = await context.CallActivityAsync<string>(
                nameof(GenerateReportActivity),
                new { agentState, decisions, toolCalls = input.ToolCalls },
                GetDefaultRetryPolicy());
            
            return new MissionResult(
                input.MissionParameters.MissionId,
                Success: true,
                DebrisCaptured: agentState.CapturedDebris.Count,
                FuelUsed: input.MissionParameters.FuelBudget - agentState.FuelRemaining,
                TotalSteps: input.MissionParameters.StepNumber + 1,
                Decisions: decisions,
                ToolCalls: input.ToolCalls,
                Summary: finalReport,
                LessonsLearned: ExtractLessons(decisions, input.ToolCalls)
            );
        }
        
        // Execute chosen tool based on agent's decision
        ToolCall toolCall;
        
        if (decision.ChosenAction == "REQUEST_HUMAN_APPROVAL")
        {
            // First call the activity to store the approval request
            await context.CallActivityAsync(
                nameof(RequestHumanApprovalActivity),
                decision.ActionParameters,
                GetDefaultRetryPolicy());
            
            // Wait for external event with 1 minute timeout
            var approvalTask = context.WaitForExternalEventAsync<HumanApproval>("HumanApproval");
            var timeoutTask = context.CreateTimer(context.CurrentUtcDateTime.AddMinutes(1), CancellationToken.None);
            
            var completedTask = await Task.WhenAny(approvalTask, timeoutTask);
            
            HumanApproval approval;
            if (completedTask == approvalTask)
            {
                approval = await approvalTask;
            }
            else
            {
                // Timeout - default to disapproved
                approval = new HumanApproval(
                    Approved: false,
                    Reason: "Approval timeout - no response within 1 minute"
                );
            }
            
            toolCall = new ToolCall(
                nameof(RequestHumanApprovalActivity),
                decision.ActionParameters,
                new ApprovalResult(
                    approval.Approved,
                    approval.Reason,
                    DateTime.UtcNow
                ),
                true,
                null
            );
        }
        else
        {
            toolCall = decision.ChosenAction switch
            {
                "SCAN_DEBRIS_FIELD" => await ExecuteTool<DebrisField>(
                    context, nameof(ScanDebrisFieldActivity), 
                    decision.ActionParameters),
                
                "ANALYZE_DEBRIS" => await ExecuteTool<DebrisAnalysis>(
                    context, nameof(AnalyzeDebrisActivity), 
                    decision.ActionParameters),
                
                "MOVE_TO_LOCATION" => await ExecuteTool<NavigationResult>(
                    context, nameof(MoveToLocationActivity), 
                    decision.ActionParameters),
                
                "CHECK_FUEL" => await ExecuteTool<FuelStatus>(
                    context, nameof(CheckFuelActivity), 
                    decision.ActionParameters),
                
                "CAPTURE_DEBRIS" => await ExecuteTool<CaptureResult>(
                    context, nameof(CaptureDebrisActivity), 
                    decision.ActionParameters),
                
                _ => new ToolCall(
                    "UNKNOWN", 
                    decision.ActionParameters, 
                    null, 
                    false, 
                    $"Unknown action: {decision.ChosenAction}")
            };
        }
        
        var toolCalls = input.ToolCalls.Append(toolCall).ToList();
        
        // Update agent state based on tool result
        agentState = UpdateAgentState(agentState, decision, toolCall);
        
        // Check for failure conditions
        if (agentState.FuelRemaining <= 0)
        {
            return new MissionResult(
                input.MissionParameters.MissionId,
                Success: false,
                DebrisCaptured: agentState.CapturedDebris.Count,
                FuelUsed: input.MissionParameters.FuelBudget,
                TotalSteps: input.MissionParameters.StepNumber + 1,
                Decisions: decisions,
                ToolCalls: toolCalls,
                Summary: "Mission aborted - fuel exhausted",
                LessonsLearned: ExtractLessons(decisions, toolCalls)
            );
        }
        
        // Check if max steps reached
        if (input.MissionParameters.StepNumber + 1 >= MAX_STEPS)
        {
            return new MissionResult(
                input.MissionParameters.MissionId,
                Success: false,
                DebrisCaptured: agentState.CapturedDebris.Count,
                FuelUsed: input.MissionParameters.FuelBudget - agentState.FuelRemaining,
                TotalSteps: input.MissionParameters.StepNumber + 1,
                Decisions: decisions,
                ToolCalls: toolCalls,
                Summary: "Mission incomplete - maximum steps reached",
                LessonsLearned: ExtractLessons(decisions, toolCalls)
            );
        }
        
        // Continue to next step
        var nextInput = new SpaceDebrisCleanupWorkflowInput(
            input.MissionParameters with { StepNumber = input.MissionParameters.StepNumber + 1 },
            agentState,
            decisions,
            toolCalls
        );
        
        context.ContinueAsNew(nextInput);
        return null!; // This line will never be reached
    }
    
    private async Task<ToolCall> ExecuteTool<TResult>(
        WorkflowContext context,
        string activityName,
        Dictionary<string, object> parameters)
    {
        try
        {
            var result = await context.CallActivityAsync<TResult>(
                activityName,
                parameters,
                GetDefaultRetryPolicy());
            
            return new ToolCall(
                activityName,
                parameters,
                result!,
                Success: true,
                ErrorMessage: null
            );
        }
        catch (Exception ex)
        {
            return new ToolCall(
                activityName,
                parameters,
                null!,
                Success: false,
                ErrorMessage: ex.Message
            );
        }
    }
    
    private AgentState UpdateAgentState(
        AgentState state, 
        AgentDecision decision, 
        ToolCall toolCall)
    {
        // Update based on tool results
        var newState = state;
        
        if (toolCall.ToolName == nameof(MoveToLocationActivity) && toolCall.Success)
        {
            var navResult = (NavigationResult)toolCall.Result;
            newState = newState with 
            { 
                Position = navResult.NewPosition,
                FuelRemaining = newState.FuelRemaining - navResult.FuelUsed
            };
        }
        else if (toolCall.ToolName == nameof(CaptureDebrisActivity) && toolCall.Success)
        {
            var captureResult = (CaptureResult)toolCall.Result;
            var captured = new List<string>(newState.CapturedDebris);
            captured.Add(captureResult.DebrisId);
            newState = newState with 
            { 
                CapturedDebris = captured,
                FuelRemaining = newState.FuelRemaining - captureResult.FuelUsed
            };
        }
        
        // Add decision to history
        var history = new List<string>(newState.DecisionHistory);
        history.Add($"Step {decision.StepNumber}: {decision.ChosenAction}");
        newState = newState with { DecisionHistory = history };
        
        return newState;
    }
    
    private List<string> ExtractLessons(
        List<AgentDecision> decisions, 
        List<ToolCall> toolCalls)
    {
        var lessons = new List<string>();
        
        // Analyze failures
        var failures = toolCalls.Where(tc => !tc.Success).ToList();
        if (failures.Any())
        {
            lessons.Add($"Encountered {failures.Count} tool failures - improve error handling");
        }
        
        // Analyze efficiency
        var avgSteps = decisions.Count;
        if (avgSteps > 30)
        {
            lessons.Add("High step count - optimize planning efficiency");
        }
        
        return lessons;
    }
}
