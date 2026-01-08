using Dapr.Workflow;
using SpaceDebrisAgent.Models;

namespace SpaceDebrisAgent.Activities.Tools;

public class MoveToLocationActivity : WorkflowActivity<Dictionary<string, object>, NavigationResult>
{
    public override async Task<NavigationResult> RunAsync(
        WorkflowActivityContext context, 
        Dictionary<string, object> parameters)
    {
        var targetX = Convert.ToDouble(parameters["x"]);
        var targetY = Convert.ToDouble(parameters["y"]);
        var targetZ = Convert.ToDouble(parameters["z"]);
        
        var newPosition = new[] { targetX, targetY, targetZ };
        
        // Calculate fuel based on distance (simplified)
        var distance = Math.Sqrt(targetX * targetX + targetY * targetY + targetZ * targetZ);
        var fuelUsed = distance * 0.1; // 0.1 kg per km
        
        await Task.Delay(50); // Simulate travel time
        
        return new NavigationResult(
            newPosition,
            fuelUsed,
            TimeSeconds: (int)(distance * 10),
            Success: true
        );
    }
}
