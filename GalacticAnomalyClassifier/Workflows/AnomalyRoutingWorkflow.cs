using Dapr.Workflow;
using GalacticAnomalyClassifier.Models;
using GalacticAnomalyClassifier.Activities;
using System.Text.Json;

namespace GalacticAnomalyClassifier.Workflows;

public class AnomalyRoutingWorkflow : Workflow<SpaceAnomaly, AnalysisResult>
{
    public override async Task<AnalysisResult> RunAsync(
        WorkflowContext context, 
        SpaceAnomaly input)
    {
        var classification = await context.CallActivityAsync<AnomalyClassification>(
            nameof(ClassifyAnomalyActivity),
            input);

        var anomalyType = ParseAnomalyType(classification.Type);
        
        string specializedAnalysis;
        Dictionary<string, object> metrics;
        List<string> recommendations;
        string threatLevel;
        
        switch (anomalyType)
        {
            case AnomalyType.TemporalRift:
                var temporalResult = await context.CallActivityAsync<TemporalAnalysis>(
                    nameof(AnalyzeTemporalRiftActivity),
                    input);
                specializedAnalysis = temporalResult.Analysis;
                metrics = temporalResult.QuantumMetrics;
                recommendations = temporalResult.SafetyProtocols;
                threatLevel = temporalResult.TimelineStability;
                break;
                
            case AnomalyType.DarkMatterCluster:
                var darkMatterResult = await context.CallActivityAsync<DarkMatterAnalysis>(
                    nameof(AnalyzeDarkMatterActivity),
                    input);
                specializedAnalysis = darkMatterResult.Analysis;
                metrics = darkMatterResult.GravitationalData;
                recommendations = darkMatterResult.HarvestingOpportunities;
                threatLevel = darkMatterResult.CollapseProbability;
                break;
                
            case AnomalyType.AlienArtifact:
                var artifactResult = await context.CallActivityAsync<ArtifactAnalysis>(
                    nameof(AnalyzeAlienArtifactActivity),
                    input);
                specializedAnalysis = artifactResult.Analysis;
                metrics = artifactResult.XenoarchaeologyData;
                recommendations = artifactResult.ExtractionProcedures;
                threatLevel = artifactResult.HostilityIndicators;
                break;
                
            case AnomalyType.StellarPhenomenon:
                var stellarResult = await context.CallActivityAsync<StellarAnalysis>(
                    nameof(AnalyzeStellarPhenomenonActivity),
                    input);
                specializedAnalysis = stellarResult.Analysis;
                metrics = stellarResult.AstrophysicsData;
                recommendations = stellarResult.ObservationProtocols;
                threatLevel = stellarResult.RadiationLevel;
                break;
                
            case AnomalyType.DimensionalTear:
                var dimensionalResult = await context.CallActivityAsync<DimensionalAnalysis>(
                    nameof(AnalyzeDimensionalTearActivity),
                    input);
                specializedAnalysis = dimensionalResult.Analysis;
                metrics = dimensionalResult.MultiverseMetrics;
                recommendations = dimensionalResult.ContainmentProcedures;
                threatLevel = dimensionalResult.RealityStability;
                break;
                
            default:
                specializedAnalysis = "Unknown anomaly type - general observation recommended";
                metrics = new Dictionary<string, object>();
                recommendations = new List<string> { "Maintain safe distance", "Continue monitoring" };
                threatLevel = "UNKNOWN";
                break;
        }
        
        return new AnalysisResult(
            input.AnomalyId,
            anomalyType,
            classification.Type,
            specializedAnalysis,
            metrics,
            recommendations,
            threatLevel
        );
    }
    
    private AnomalyType ParseAnomalyType(string type)
    {
        return type.ToLower() switch
        {
            var t when t.Contains("temporal") || t.Contains("time") => AnomalyType.TemporalRift,
            var t when t.Contains("dark matter") => AnomalyType.DarkMatterCluster,
            var t when t.Contains("artifact") || t.Contains("alien") => AnomalyType.AlienArtifact,
            var t when t.Contains("stellar") || t.Contains("star") => AnomalyType.StellarPhenomenon,
            var t when t.Contains("dimensional") || t.Contains("tear") => AnomalyType.DimensionalTear,
            _ => AnomalyType.Unknown
        };
    }
}
