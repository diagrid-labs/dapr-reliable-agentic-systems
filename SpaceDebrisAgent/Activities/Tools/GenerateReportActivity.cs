using Dapr.Workflow;
using SpaceDebrisAgent.Models;
using System.Text;

namespace SpaceDebrisAgent.Activities.Tools;

public class GenerateReportActivity : WorkflowActivity<dynamic, string>
{
    public override Task<string> RunAsync(
        WorkflowActivityContext context, 
        dynamic input)
    {
        var agentState = (AgentState)input.agentState;
        var decisions = (List<AgentDecision>)input.decisions;
        var toolCalls = (List<ToolCall>)input.toolCalls;
        
        var report = new StringBuilder();
        report.AppendLine("=== MISSION COMPLETE ===");
        report.AppendLine($"Total Steps: {decisions.Count}");
        report.AppendLine($"Debris Captured: {agentState.CapturedDebris.Count}");
        report.AppendLine($"Fuel Remaining: {agentState.FuelRemaining:F2} kg");
        report.AppendLine();
        report.AppendLine("CAPTURED DEBRIS:");
        foreach (var debris in agentState.CapturedDebris)
        {
            report.AppendLine($"  - {debris}");
        }
        report.AppendLine();
        report.AppendLine("KEY DECISIONS:");
        foreach (var decision in decisions.TakeLast(5))
        {
            report.AppendLine($"  Step {decision.StepNumber}: {decision.ChosenAction}");
            report.AppendLine($"    Reasoning: {decision.Reasoning}");
        }
        
        return Task.FromResult(report.ToString());
    }
}
