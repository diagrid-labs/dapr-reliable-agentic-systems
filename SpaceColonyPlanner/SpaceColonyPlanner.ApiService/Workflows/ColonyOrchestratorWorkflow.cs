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
        void Publish(string stage) =>
            context.SetCustomStatus(new ColonyProgress(stage, structures.ToList()));

        Publish("AnalyzingPlanet");

        // Step 1: Analyze planet to understand constraints
        var planetAnalysis = await context.CallActivityAsync<PlanetAnalysis>(
            nameof(AnalyzePlanetActivity),
            input.Planet,
            GetDefaultRetryPolicy());
        
        Publish("DeterminingStructures");

        // Step 2: Orchestrator determines what structures are needed
        // This is DYNAMIC - different planets need different structures
        var structureRequests = await context.CallActivityAsync<List<StructureRequest>>(
            nameof(DetermineStructuresActivity),
            new DetermineStructuresInput(input.Planet, input.Requirements, planetAnalysis),
            GetDefaultRetryPolicy());
        
        structures.AddRange(structureRequests.Select(r =>
            new StructureProgress(r.StructureType, r.Priority.ToString(), r.Quantity, false)));
        Publish("PlanningStructures");

        // Step 3: Dynamically spawn worker tasks for each structure type
        // The orchestrator doesn't know ahead of time how many workers needed!
        var workerTasks = new List<Task<StructurePlan>>();
        
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
                context.CallActivityAsync<StructurePlan>(workerActivity, workerInput, GetDefaultRetryPolicy())
            );
        }
        
        // Publish each worker as it completes, the plans keep the request order
        var pending = workerTasks.ToList();
        while (pending.Count > 0)
        {
            var done = await Task.WhenAny(pending);
            pending.Remove(done);
            await done;
            var index = workerTasks.IndexOf(done);
            structures[index] = structures[index] with { Done = true };
            Publish("PlanningStructures");
        }

        // All workers are complete here, this only collects the plans in the request order
        var structurePlans = await Task.WhenAll(workerTasks);

        Publish("Synthesizing");
        
        // Step 4: Synthesize individual plans into master plan
        var masterPlan = await context.CallActivityAsync<ColonyMasterPlan>(
            nameof(SynthesizePlanActivity),
            new SynthesizePlanInput(input.Planet.PlanetId, structurePlans.ToList()),
            GetDefaultRetryPolicy());
        
        Publish("Optimizing");

        // Step 5: Optimize construction timeline
        var optimizedPlan = await context.CallActivityAsync<ColonyMasterPlan>(
            nameof(OptimizeTimelineActivity),
            masterPlan,
            GetDefaultRetryPolicy());
        
        return optimizedPlan;
    }
}
