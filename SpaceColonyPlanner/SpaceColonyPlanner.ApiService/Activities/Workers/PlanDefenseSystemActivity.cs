using Dapr.Workflow;
using SpaceColonyPlanner.Models;

namespace SpaceColonyPlanner.Activities.Workers;

public class PlanDefenseSystemActivity : WorkflowActivity<WorkerInput, StructurePlan>
{
    public override Task<StructurePlan> RunAsync(
        WorkflowActivityContext context, 
        WorkerInput input)
    {
        // Simplified defense system plan
        return Task.FromResult(new StructurePlan(
            "DefenseSystem",
            input.Request.Quantity,
            ["Defensive perimeter sensors", "Shield generators", "Automated defense turrets", "Command center"],
            180,
            12000,
            ["Power supply", "Communication network", "Strategic positioning"],
            "Integrated defense system with early warning sensors, energy shields, and automated response capabilities."
        ));
    }
}
