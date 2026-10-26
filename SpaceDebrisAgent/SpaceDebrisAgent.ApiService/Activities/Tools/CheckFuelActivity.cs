using Dapr.Workflow;
using SpaceDebrisAgent.Models;
using System.Text.Json;

namespace SpaceDebrisAgent.Activities.Tools;

public class CheckFuelActivity : WorkflowActivity<Dictionary<string, object>, FuelStatus>
{
    public override Task<FuelStatus> RunAsync(
        WorkflowActivityContext context, 
        Dictionary<string, object> parameters)
    {
        // The workflow passes the tracked fuel level, in a real implementation this would query fuel sensors
        var currentFuel = ((JsonElement)parameters["fuelRemaining"]).GetDouble();
        var maxFuel = ((JsonElement)parameters["fuelBudget"]).GetDouble();
        var percentRemaining = (currentFuel / maxFuel) * 100;
        var estimatedRange = currentFuel * 10; // 10 km per kg, matches MoveToLocationActivity
        
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
