using Dapr.Workflow;
using SpaceColonyPlanner.Models;

namespace SpaceColonyPlanner.Activities.Workers;

public class UnknownStructureActivity : WorkflowActivity<WorkerInput, StructurePlan>
{
    private readonly ILogger<UnknownStructureActivity> _logger;

    public UnknownStructureActivity(ILogger<UnknownStructureActivity> logger)
    {
        _logger = logger;
    }

    public override Task<StructurePlan> RunAsync(
        WorkflowActivityContext context,
        WorkerInput input)
    {
        _logger.LogWarning(
            "Unknown structure type '{StructureType}' requested for planet {PlanetId}. Reasoning: {Reasoning}",
            input.Request.StructureType,
            input.Planet.PlanetId,
            input.Request.Reasoning);

        var plan = new StructurePlan(
            StructureType: input.Request.StructureType,
            Quantity: input.Request.Quantity,
            Materials: [],
            ConstructionDays: 0,
            WorkerHours: 0,
            Prerequisites: [],
            DetailedSpecification: $"UNKNOWN STRUCTURE TYPE: '{input.Request.StructureType}' requires manual review. " +
                $"Priority: {input.Request.Priority}. " +
                $"Requested quantity: {input.Request.Quantity}. " +
                $"Original reasoning: {input.Request.Reasoning}"
        );

        return Task.FromResult(plan);
    }
}
