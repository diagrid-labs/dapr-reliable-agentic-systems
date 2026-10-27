using Dapr.Workflow;
using SpaceColonyPlanner.Models;
using SpaceColonyPlanner.Activities.Analysis;
using SpaceColonyPlanner.Activities.Workers;

namespace SpaceColonyPlanner.Workflows;

public class ColonyOrchestratorWorkflow : Workflow<ColonyRequest, ColonyMasterPlan>
{
    private static WorkflowTaskOptions GetDefaultRetryPolicy()
    {
        return new WorkflowTaskOptions(
            new WorkflowRetryPolicy(
                maxNumberOfAttempts: 5,
                firstRetryInterval: TimeSpan.FromSeconds(1)));
    }

    public override async Task<ColonyMasterPlan> RunAsync(
        WorkflowContext context, 
        ColonyRequest input)
    {
        var structures = new List<StructureProgress>();
        var usage = TokenUsage.Zero;
        void Publish(string stage) =>
            context.SetCustomStatus(new ColonyProgress(stage, structures.ToList(), usage));

        Publish("AnalyzingPlanet");

        // Step 1: Analyze planet to understand constraints
        var analyzed = await context.CallActivityAsync<Metered<PlanetAnalysis>>(
            nameof(AnalyzePlanetActivity),
            input.Planet,
            GetDefaultRetryPolicy());
        var planetAnalysis = analyzed.Value;
        usage += analyzed.Usage;
        
        Publish("DeterminingStructures");

        // Step 2: Orchestrator determines what structures are needed
        // This is DYNAMIC - different planets need different structures
        var determined = await context.CallActivityAsync<Metered<List<StructureRequest>>>(
            nameof(DetermineStructuresActivity),
            new DetermineStructuresInput(input.Planet, input.Requirements, planetAnalysis),
            GetDefaultRetryPolicy());
        var structureRequests = determined.Value;
        usage += determined.Usage;
        
        structures.AddRange(structureRequests.Select(r =>
            new StructureProgress(r.StructureType, r.Priority.ToString(), r.Quantity, false)));
        Publish("PlanningStructures");

        // Step 3: Dynamically spawn worker tasks for each structure type
        // The orchestrator doesn't know ahead of time how many workers needed!
        var workerTasks = new List<Task<Metered<StructurePlan>>>();
        
        foreach (var request in structureRequests)
        {
            // Route to appropriate specialist worker based on structure type
            var workerActivity = request.StructureType switch
            {
                "HabitatDome" => nameof(PlanHabitatDomeActivity),
                "PowerPlant" => nameof(PlanPowerPlantActivity),
                "Agriculture" => nameof(PlanAgricultureActivity),
                "MiningFacility" => nameof(PlanMiningFacilityActivity),
                "ResearchLab" => nameof(PlanResearchLabActivity),
                "DefenseSystem" => nameof(PlanDefenseSystemActivity),
                _ => nameof(UnknownStructureActivity)
            };
            
            var workerInput = new WorkerInput(
                request,
                input.Planet,
                planetAnalysis
            );
            
            workerTasks.Add(
                context.CallActivityAsync<Metered<StructurePlan>>(workerActivity, workerInput, GetDefaultRetryPolicy())
            );
        }
        
        // Publish each worker as it completes, the plans keep the request order
        await ProcessAsCompletedAsync(workerTasks, (index, metered) =>
        {
            usage += metered.Usage;
            structures[index] = structures[index] with { Done = true };
            Publish("PlanningStructures");
        });

        // All workers are complete here, this only collects the plans in the request order
        var structurePlans = (await Task.WhenAll(workerTasks)).Select(m => m.Value).ToList();

        Publish("Synthesizing");
        
        // Step 4: Synthesize individual plans into master plan
        var synthesized = await context.CallActivityAsync<Metered<ColonyMasterPlan>>(
            nameof(SynthesizePlanActivity),
            new SynthesizePlanInput(input.Planet.PlanetId, structurePlans),
            GetDefaultRetryPolicy());
        var masterPlan = synthesized.Value;
        usage += synthesized.Usage;
        
        Publish("Optimizing");

        // Step 5: Optimize construction timeline
        var optimizedPlan = await context.CallActivityAsync<ColonyMasterPlan>(
            nameof(OptimizeTimelineActivity),
            masterPlan,
            GetDefaultRetryPolicy());
        
        return optimizedPlan;
    }

    // A while loop inside a workflow is not a recommended practice: the loop runs again on every
    // replay and an unbounded loop keeps growing the workflow history. It is used here only so the
    // web front-end gets a status update as soon as each task completes. The loop is bounded by
    // the number of tasks, which are all scheduled before it starts.
    private static async Task ProcessAsCompletedAsync<T>(List<Task<T>> tasks, Action<int, T> onCompleted)
    {
        var pending = tasks.ToList();
        while (pending.Count > 0)
        {
            var done = await Task.WhenAny(pending);
            pending.Remove(done);
            onCompleted(tasks.IndexOf(done), await done);
        }
    }
}
