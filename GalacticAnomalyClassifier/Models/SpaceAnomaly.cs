namespace GalacticAnomalyClassifier.Models;

public record SpaceAnomaly(
    string AnomalyId,
    string SensorData,
    string Coordinates,
    DateTime DetectedAt,
    Dictionary<string, double> Measurements
);

public record AnomalyClassification(
    string Type,
    double Confidence,
    string Reasoning
);

public enum AnomalyType
{
    TemporalRift,
    DarkMatterCluster,
    AlienArtifact,
    StellarPhenomenon,
    DimensionalTear,
    Unknown
}

public record AnalysisResult(
    string AnomalyId,
    AnomalyType Type,
    string Classification,
    string SpecializedAnalysis,
    Dictionary<string, object> Metrics,
    List<string> Recommendations,
    string ThreatLevel
);
