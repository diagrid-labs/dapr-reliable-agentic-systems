namespace AnomalyAnalysis.Models;

public record SpatialAnomaly(
    string AnomalyId,
    string RawSensorData,
    string SpatialCoordinates,
    DateTime DetectedAt,
    Dictionary<string, double> SensorReadings
);

public record AnalysisStage(
    string StageName,
    string Input,
    string Output,
    bool Success,
    string? ErrorMessage = null
);

public record AnalysisResult(
    string AnomalyId,
    List<AnalysisStage> Stages,
    string AnomalyType,
    string ScientificAnalysis,
    string RiskLevel,
    string TacticalRecommendation
);
