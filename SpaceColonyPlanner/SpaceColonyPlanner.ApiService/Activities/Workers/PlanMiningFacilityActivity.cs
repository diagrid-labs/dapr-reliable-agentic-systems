using Dapr.Workflow;
using SpaceColonyPlanner.Models;
using System.Text.Json;

namespace SpaceColonyPlanner.Activities.Workers;

public class PlanMiningFacilityActivity : WorkflowActivity<WorkerInput, Metered<StructurePlan>>
{
    public override Task<Metered<StructurePlan>> RunAsync(
        WorkflowActivityContext context, 
        WorkerInput input)
    {
        // Simplified mining facility plan
        return Task.FromResult(new Metered<StructurePlan>(new StructurePlan(
            "MiningFacility",
            input.Request.Quantity,
            ["Mining equipment", "Ore processing units", "Storage silos", "Transport systems"],
            200,
            15000,
            ["Power supply", "Access roads", "Water supply"],
            "Automated mining and processing facility for resource extraction with minimal human oversight."
        ), TokenUsage.Zero));
    }
}
