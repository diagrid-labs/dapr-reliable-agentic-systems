using Dapr.Workflow;
using SpaceColonyPlanner.Models;

namespace SpaceColonyPlanner.Activities.Workers;

public class PlanResearchLabActivity : WorkflowActivity<WorkerInput, StructurePlan>
{
    public override Task<StructurePlan> RunAsync(
        WorkflowActivityContext context, 
        WorkerInput input)
    {
        // Simplified research lab plan
        return Task.FromResult(new StructurePlan(
            "ResearchLab",
            input.Request.Quantity,
            ["Scientific equipment", "Clean room modules", "Data processing centers", "Sample storage"],
            150,
            10000,
            ["Power supply", "Climate control", "Secure foundation"],
            "Multi-purpose research laboratory for planetary studies, biology, and technology development."
        ));
    }
}
