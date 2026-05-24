using Dapr.Workflow;
using SpaceDebrisAgent.Models;

namespace SpaceDebrisAgent.Activities.Tools;

public class CaptureDebrisActivity : WorkflowActivity<Dictionary<string, object>, CaptureResult>
{
    public override async Task<CaptureResult> RunAsync(
        WorkflowActivityContext context, 
        Dictionary<string, object> parameters)
    {
        var debrisId = parameters["debrisId"].ToString()!;
        
        // Simulate capture attempt with 80% success rate
        var random = new Random();
        var success = random.NextDouble() > 0.2;
        var fuelUsed = random.NextDouble() * 5.0; // 0-5 kg
        
        await Task.Delay(100); // Simulate operation time
        
        return new CaptureResult(
            debrisId,
            success,
            fuelUsed,
            success 
                ? $"Successfully captured debris {debrisId}" 
                : $"Capture failed - debris {debrisId} tumbling too fast"
        );
    }
}
