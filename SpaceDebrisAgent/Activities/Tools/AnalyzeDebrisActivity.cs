using Dapr.Workflow;
using SpaceDebrisAgent.Models;

namespace SpaceDebrisAgent.Activities.Tools;

public class AnalyzeDebrisActivity : WorkflowActivity<Dictionary<string, object>, DebrisAnalysis>
{
    public override async Task<DebrisAnalysis> RunAsync(
        WorkflowActivityContext context, 
        Dictionary<string, object> parameters)
    {
        var debrisId = parameters["debrisId"].ToString()!;
        
        // Simulate debris analysis
        await Task.Delay(150);
        
        var random = new Random();
        var structuralIntegrity = random.NextDouble() * 100;
        
        return new DebrisAnalysis(
            debrisId,
            DetailedComposition: "Aluminum alloy with carbon fiber components",
            StructuralIntegrity: structuralIntegrity,
            CaptureRecommendation: structuralIntegrity > 50 
                ? "Safe to capture - structure intact" 
                : "High risk - debris may fragment during capture"
        );
    }
}
