using Dapr.Workflow;
using SpaceDebrisAgent.Models;

namespace SpaceDebrisAgent.Activities.Tools;

public class CheckFuelActivity : WorkflowActivity<Dictionary<string, object>, FuelStatus>
{
    public override Task<FuelStatus> RunAsync(
        WorkflowActivityContext context, 
        Dictionary<string, object> parameters)
    {
        // This would query actual fuel sensors in real implementation
        // For demo, return mock data
        var currentFuel = 75.0;
        var maxFuel = 100.0;
        var percentRemaining = (currentFuel / maxFuel) * 100;
        var estimatedRange = currentFuel * 10; // 10 km per kg
        
        var warning = percentRemaining switch
        {
            < 20 => "CRITICAL - Return to base immediately",
            < 40 => "WARNING - Plan return trajectory soon",
            _ => "Fuel levels normal"
        };
        
        return Task.FromResult(new FuelStatus(
            currentFuel,
            percentRemaining,
            estimatedRange,
            warning
        ));
    }
}
