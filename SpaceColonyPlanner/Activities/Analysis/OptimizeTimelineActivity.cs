using Dapr.Workflow;
using SpaceColonyPlanner.Models;

namespace SpaceColonyPlanner.Activities.Analysis;

public class OptimizeTimelineActivity : WorkflowActivity<ColonyMasterPlan, ColonyMasterPlan>
{
    public override Task<ColonyMasterPlan> RunAsync(
        WorkflowActivityContext context, 
        ColonyMasterPlan input)
    {
        // For now, just pass through the master plan
        // In a real implementation, this could optimize the construction timeline
        return Task.FromResult(input);
    }
}
