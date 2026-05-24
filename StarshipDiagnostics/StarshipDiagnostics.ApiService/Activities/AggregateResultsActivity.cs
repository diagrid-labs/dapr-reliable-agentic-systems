using Dapr.Workflow;
using StarshipDiagnostics.Models;

namespace StarshipDiagnostics.Activities;

public class AggregateResultsActivity : WorkflowActivity<AggregateResultsInput, DiagnosticReport>
{
    public override async Task<DiagnosticReport> RunAsync(
        WorkflowActivityContext context,
        AggregateResultsInput input)
    {
        // Determine overall status
        var hasCritical = input.ScanResults.Any(s => s.Status == "CRITICAL");
        var hasWarning = input.ScanResults.Any(s => s.Status == "WARNING");

        string overallStatus = hasCritical ? "CRITICAL" :
                              hasWarning ? "WARNING" : "OK";

        // Check if any votes resulted in IMMEDIATE_GROUNDING consensus
        var mustGround = input.VoteResults.Any(v =>
            v.Consensus == "IMMEDIATE_GROUNDING");

        // Collect required repairs
        var requiredRepairs = input.ScanResults
            .Where(s => s.Status != "OK")
            .SelectMany(s => s.Recommendations ?? new List<string>())
            .Distinct()
            .ToList();

        // Estimate repair time
        int estimatedHours = input.ScanResults.Count(s => s.Status == "CRITICAL") * 24 +
                           input.ScanResults.Count(s => s.Status == "WARNING") * 4;

        return await Task.FromResult(new DiagnosticReport(
            "SHIP-ID",
            DateTime.UtcNow,
            input.ScanResults,
            input.VoteResults,
            overallStatus,
            !mustGround && !hasCritical,
            requiredRepairs,
            estimatedHours
        ));
    }
}
