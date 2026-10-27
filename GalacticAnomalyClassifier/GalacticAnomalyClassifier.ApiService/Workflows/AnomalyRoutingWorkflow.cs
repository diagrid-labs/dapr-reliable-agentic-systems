using Dapr.Workflow;
using GalacticAnomalyClassifier.Models;
using GalacticAnomalyClassifier.Activities;
using System.Text.Json;

namespace GalacticAnomalyClassifier.Workflows;

public class AnomalyRoutingWorkflow : Workflow<SpaceAnomaly, AnalysisResult>
{
    private static WorkflowTaskOptions GetDefaultRetryPolicy()
    {
        return new WorkflowTaskOptions(
            new WorkflowRetryPolicy(
                maxNumberOfAttempts: 5,
                firstRetryInterval: TimeSpan.FromSeconds(1)));
    }

    public override async Task<AnalysisResult> RunAsync(
        WorkflowContext context, 
        SpaceAnomaly input)
    {
        context.SetCustomStatus(new RoutingProgress("Classifying", null, null, TokenUsage.Zero));

        var classifiedAnomaly = await context.CallActivityAsync<Metered<AnomalyClassification>>(
            nameof(ClassifyAnomalyActivity),
            input,
            GetDefaultRetryPolicy());

        var classification = classifiedAnomaly.Value;
        var usage = classifiedAnomaly.Usage;

        var anomalyType = ParseAnomalyType(classification.Type);
        context.SetCustomStatus(new RoutingProgress("Analyzing", anomalyType.ToString(), classification.Confidence, usage));

        string specializedAnalysis;
        Dictionary<string, object> metrics;
        List<string> recommendations;
        string threatLevel;
        
        switch (anomalyType)
        {
            case AnomalyType.TemporalRift:
                var temporalResultMetered = await context.CallActivityAsync<Metered<TemporalAnalysis>>(
                    nameof(AnalyzeTemporalRiftActivity),
                    input,
                    GetDefaultRetryPolicy());
                var temporalResult = temporalResultMetered.Value;
                usage += temporalResultMetered.Usage;
                specializedAnalysis = temporalResult.Analysis;
                metrics = temporalResult.QuantumMetrics;
                recommendations = temporalResult.SafetyProtocols;
                threatLevel = temporalResult.TimelineStability;
                break;
                
            case AnomalyType.DarkMatterCluster:
                var darkMatterResultMetered = await context.CallActivityAsync<Metered<DarkMatterAnalysis>>(
                    nameof(AnalyzeDarkMatterActivity),
                    input,
                    GetDefaultRetryPolicy());
                var darkMatterResult = darkMatterResultMetered.Value;
                usage += darkMatterResultMetered.Usage;
                specializedAnalysis = darkMatterResult.Analysis;
                metrics = darkMatterResult.GravitationalData;
                recommendations = darkMatterResult.HarvestingOpportunities;
                threatLevel = darkMatterResult.CollapseProbability;
                break;
                
            case AnomalyType.AlienArtifact:
                var artifactResultMetered = await context.CallActivityAsync<Metered<ArtifactAnalysis>>(
                    nameof(AnalyzeAlienArtifactActivity),
                    input,
                    GetDefaultRetryPolicy());
                var artifactResult = artifactResultMetered.Value;
                usage += artifactResultMetered.Usage;
                specializedAnalysis = artifactResult.Analysis;
                metrics = artifactResult.XenoarchaeologyData;
                recommendations = artifactResult.ExtractionProcedures;
                threatLevel = artifactResult.HostilityIndicators;
                break;
                
            case AnomalyType.StellarPhenomenon:
                var stellarResultMetered = await context.CallActivityAsync<Metered<StellarAnalysis>>(
                    nameof(AnalyzeStellarPhenomenonActivity),
                    input,
                    GetDefaultRetryPolicy());
                var stellarResult = stellarResultMetered.Value;
                usage += stellarResultMetered.Usage;
                specializedAnalysis = stellarResult.Analysis;
                metrics = stellarResult.AstrophysicsData;
                recommendations = stellarResult.ObservationProtocols;
                threatLevel = stellarResult.RadiationLevel;
                break;
                
            case AnomalyType.DimensionalTear:
                var dimensionalResultMetered = await context.CallActivityAsync<Metered<DimensionalAnalysis>>(
                    nameof(AnalyzeDimensionalTearActivity),
                    input,
                    GetDefaultRetryPolicy());
                var dimensionalResult = dimensionalResultMetered.Value;
                usage += dimensionalResultMetered.Usage;
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
        
        context.SetCustomStatus(new RoutingProgress("Analyzing", anomalyType.ToString(), classification.Confidence, usage));

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
