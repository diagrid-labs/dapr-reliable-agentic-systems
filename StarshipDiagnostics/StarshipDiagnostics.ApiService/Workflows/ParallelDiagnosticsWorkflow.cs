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
        var usage = TokenUsage.Zero;
        void Publish(string stage) =>
            context.SetCustomStatus(new DiagnosticsProgress(stage, scans.ToList(), votesProgress.ToList(), usage));

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
            .Select(activity => context.CallActivityAsync<Metered<ScanResult>>(activity, input, GetDefaultRetryPolicy()))
            .ToList();

        // Publish each scan as it completes, the results keep the original order
        await ProcessAsCompletedAsync(scanTasks, (index, metered) =>
        {
            var result = metered.Value;
            usage += metered.Usage;
            scans.Add(new ScanProgress(
                scanActivities[index],
                result.SubsystemName,
                result.Status,
                result.HealthPercentage));
            Publish("Scanning");
        });

        // All scans are complete here, this only collects the results in the original order
        var scanResults = (await Task.WhenAll(scanTasks)).Select(m => m.Value).ToList();

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
                var voteTasks = new List<Task<Metered<KeyValuePair<string, string>>>>
                {
                    context.CallActivityAsync<Metered<KeyValuePair<string, string>>>(
                        nameof(SafetyVoterActivity),
                        finding,
                        GetDefaultRetryPolicy()),
                    context.CallActivityAsync<Metered<KeyValuePair<string, string>>>(
                        nameof(SeverityVoterActivity),
                        finding,
                        GetDefaultRetryPolicy()),
                    context.CallActivityAsync<Metered<KeyValuePair<string, string>>>(
                        nameof(RecommendationVoterActivity),
                        finding,
                        GetDefaultRetryPolicy())
                };

                var metered = await Task.WhenAll(voteTasks);
                usage = metered.Aggregate(usage, (sum, m) => sum + m.Usage);
                var votes = metered.Select(m => m.Value);

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
