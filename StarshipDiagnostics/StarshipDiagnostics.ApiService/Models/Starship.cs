namespace StarshipDiagnostics.Models;

public record Starship(
    string ShipId,
    string Name,
    string Class,
    int YearsInService,
    DateTime LastMaintenance,
    Dictionary<string, object> Telemetry
);

public record ScanResult(
    string SubsystemName,
    string? Status,
    double HealthPercentage,
    List<string>? Issues,
    List<string>? Recommendations,
    string? DetailedAnalysis
);

public record VoteResult(
    string Category,
    Dictionary<string, string> Votes,
    string Consensus,
    double AgreementLevel
);

public record AggregateResultsInput(
    string ShipId,
    List<ScanResult> ScanResults,
    List<VoteResult> VoteResults
);

public record DiagnosticReport(
    string ShipId,
    DateTime ScanTimestamp,
    List<ScanResult> SubsystemScans,
    List<VoteResult> CriticalVotes,
    string OverallStatus,
    bool ClearForDeparture,
    List<string> RequiredRepairs,
    int EstimatedRepairHours
);
