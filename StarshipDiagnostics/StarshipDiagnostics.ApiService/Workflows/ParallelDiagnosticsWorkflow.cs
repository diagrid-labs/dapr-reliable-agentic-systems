using Dapr.Workflow;
using StarshipDiagnostics.Activities;
using StarshipDiagnostics.Activities.Scanners;
using StarshipDiagnostics.Activities.Voters;
using StarshipDiagnostics.Models;

namespace StarshipDiagnostics.Workflows;

public class ParallelDiagnosticsWorkflow : Workflow<Starship, DiagnosticReport>
{
    private static WorkflowTaskOptions GetDefaultRetryPolicy()
    {
        return new WorkflowTaskOptions(
            new WorkflowRetryPolicy(
                maxNumberOfAttempts: 5,
                firstRetryInterval: TimeSpan.FromSeconds(1)));
    }

    public override async Task<DiagnosticReport> RunAsync(
        WorkflowContext context,
        Starship input)
    {
        var scans = new List<ScanProgress>();
        var votesProgress = new List<VoteProgress>();
        void Publish(string stage) =>
            context.SetCustomStatus(new DiagnosticsProgress(stage, scans.ToList(), votesProgress.ToList()));

        Publish("Scanning");

        // Specify independent scans to be run in parallel
        var scanActivities = new[]
        {
            nameof(HullIntegrityScanActivity),
            nameof(ReactorCoreScanActivity),
            nameof(NavigationScanActivity),
            nameof(LifeSupportScanActivity),
            nameof(WeaponsScanActivity)
        };
        var scanTasks = scanActivities
            .Select(activity => context.CallActivityAsync<ScanResult>(activity, input, GetDefaultRetryPolicy()))
            .ToList();

        // Publish each scan as it completes, the results keep the original order
        var pending = scanTasks.ToList();
        while (pending.Count > 0)
        {
            var done = await Task.WhenAny(pending);
            pending.Remove(done);
            var result = await done;
            scans.Add(new ScanProgress(
                scanActivities[scanTasks.IndexOf(done)],
                result.SubsystemName,
                result.Status,
                result.HealthPercentage));
            Publish("Scanning");
        }

        // All scans are complete here, this only collects the results in the original order
        var scanResults = await Task.WhenAll(scanTasks);

        // Identify critical findings that need voting
        var criticalFindings = scanResults
            .Where(r => r.Status == "CRITICAL")
            .ToList();

        var voteResults = new List<VoteResult>();

        if (criticalFindings.Any())
        {
            Publish("Voting");

            // VOTING: Multiple AI models vote on critical findings
            // Run votes in parallel for each critical finding
            foreach (var finding in criticalFindings)
            {
                // Parallel voting - 3 different AI perspectives
                var voteTasks = new List<Task<KeyValuePair<string, string>>>
                {
                    context.CallActivityAsync<KeyValuePair<string, string>>(
                        nameof(SafetyVoterActivity),
                        finding,
                        GetDefaultRetryPolicy()),
                    context.CallActivityAsync<KeyValuePair<string, string>>(
                        nameof(SeverityVoterActivity),
                        finding,
                        GetDefaultRetryPolicy()),
                    context.CallActivityAsync<KeyValuePair<string, string>>(
                        nameof(RecommendationVoterActivity),
                        finding,
                        GetDefaultRetryPolicy())
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
                votesProgress.Add(new VoteProgress(finding.SubsystemName, consensus, agreement));
                Publish("Voting");
            }
        }

        Publish("Aggregating");

        // Aggregate all results
        var aggregationInput = new AggregateResultsInput(input.ShipId, scanResults.ToList(), voteResults);
        var finalReport = await context.CallActivityAsync<DiagnosticReport>(
            nameof(AggregateResultsActivity),
            aggregationInput,
            GetDefaultRetryPolicy());

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
