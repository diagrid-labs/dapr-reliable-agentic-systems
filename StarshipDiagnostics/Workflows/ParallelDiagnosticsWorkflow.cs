using Dapr.Workflow;
using StarshipDiagnostics.Activities;
using StarshipDiagnostics.Activities.Scanners;
using StarshipDiagnostics.Activities.Voters;
using StarshipDiagnostics.Models;

namespace StarshipDiagnostics.Workflows;

public class ParallelDiagnosticsWorkflow : Workflow<Starship, DiagnosticReport>
{
    public override async Task<DiagnosticReport> RunAsync(
        WorkflowContext context, 
        Starship input)
    {
        // SECTIONING: Run independent scans in parallel
        var scanTasks = new List<Task<ScanResult>>
        {
            context.CallActivityAsync<ScanResult>(
                nameof(HullIntegrityScanActivity),
                input),
            context.CallActivityAsync<ScanResult>(
                nameof(ReactorCoreScanActivity),
                input),
            context.CallActivityAsync<ScanResult>(
                nameof(NavigationScanActivity),
                input),
            context.CallActivityAsync<ScanResult>(
                nameof(LifeSupportScanActivity),
                input),
            context.CallActivityAsync<ScanResult>(
                nameof(WeaponsScanActivity),
                input)
        };
        
        // Wait for all scans to complete
        var scanResults = await Task.WhenAll(scanTasks);
        
        // Identify critical findings that need voting
        var criticalFindings = scanResults
            .Where(r => r.Status == "CRITICAL")
            .ToList();
        
        var voteResults = new List<VoteResult>();
        
        if (criticalFindings.Any())
        {
            // VOTING: Multiple AI models vote on critical findings
            // Run votes in parallel for each critical finding
            foreach (var finding in criticalFindings)
            {
                // Parallel voting - 3 different AI perspectives
                var voteTasks = new List<Task<KeyValuePair<string, string>>>
                {
                    context.CallActivityAsync<KeyValuePair<string, string>>(
                        nameof(SafetyVoterActivity),
                        finding),
                    context.CallActivityAsync<KeyValuePair<string, string>>(
                        nameof(SeverityVoterActivity),
                        finding),
                    context.CallActivityAsync<KeyValuePair<string, string>>(
                        nameof(RecommendationVoterActivity),
                        finding)
                };
                
                var votes = await Task.WhenAll(voteTasks);
                
                // Aggregate votes
                var voteDict = votes.ToDictionary(v => v.Key, v => v.Value);
                var consensus = DetermineConsensus(voteDict);
                var agreement = CalculateAgreement(voteDict);
                
                voteResults.Add(new VoteResult(
                    finding.SubsystemName,
                    voteDict,
                    consensus,
                    agreement
                ));
            }
        }
        
        // Aggregate all results
        var aggregationInput = new AggregateResultsInput(scanResults.ToList(), voteResults);
        var finalReport = await context.CallActivityAsync<DiagnosticReport>(
            nameof(AggregateResultsActivity),
            aggregationInput);
        
        return finalReport;
    }
    
    private string DetermineConsensus(Dictionary<string, string> votes)
    {
        // Majority voting
        var groups = votes.GroupBy(v => v.Value)
            .OrderByDescending(g => g.Count())
            .ToList();
        
        return groups.First().Key;
    }
    
    private double CalculateAgreement(Dictionary<string, string> votes)
    {
        var groups = votes.GroupBy(v => v.Value);
        var maxCount = groups.Max(g => g.Count());
        return (double)maxCount / votes.Count;
    }
}
