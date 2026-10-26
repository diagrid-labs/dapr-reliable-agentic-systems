using Dapr.Workflow;
using SpaceDebrisAgent.Models;
using System.Text.Json;

namespace SpaceDebrisAgent.Activities.Tools;

public class MoveToLocationActivity : WorkflowActivity<Dictionary<string, object>, NavigationResult>
{
    private const double FUEL_PER_KM = 0.1; // kg

    public override async Task<NavigationResult> RunAsync(
        WorkflowActivityContext context, 
        Dictionary<string, object> parameters)
    {
        var target = new[]
        {
            ((JsonElement)parameters["x"]).GetDouble(),
            ((JsonElement)parameters["y"]).GetDouble(),
            ((JsonElement)parameters["z"]).GetDouble()
        };
        var position = ((JsonElement)parameters["position"]).EnumerateArray().Select(p => p.GetDouble()).ToArray();
        var fuelRemaining = ((JsonElement)parameters["fuelRemaining"]).GetDouble();

        // Fuel is based on the straight-line distance from the current position (simplified)
        var distance = Math.Sqrt(target.Zip(position, (t, p) => (t - p) * (t - p)).Sum());
        var fuelNeeded = distance * FUEL_PER_KM;

        // Guardrail: refuse a move the spacecraft can't afford, instead of draining the tank
        if (fuelNeeded > fuelRemaining)
        {
            return new NavigationResult(
                position,
                FuelUsed: 0,
                TimeSeconds: 0,
                Success: false,
                Message: $"Move refused - needs {fuelNeeded:F1} kg fuel for {distance:F1} km, only {fuelRemaining:F1} kg remaining");
        }
        
        await Task.Delay(50); // Simulate travel time
        
        return new NavigationResult(
            target,
            fuelNeeded,
            TimeSeconds: (int)(distance * 10),
            Success: true,
            Message: $"Moved {distance:F1} km"
        );
    }
}
