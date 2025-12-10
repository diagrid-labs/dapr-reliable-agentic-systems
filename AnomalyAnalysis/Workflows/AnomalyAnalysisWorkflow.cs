using Dapr.Workflow;
using AnomalyAnalysis.Models;
using AnomalyAnalysis.Activities;

namespace AnomalyAnalysis.Workflows;

public class AnomalyAnalysisWorkflow : Workflow<SpatialAnomaly, AnalysisResult>
{
    public override async Task<AnalysisResult> RunAsync(
        WorkflowContext context, 
        SpatialAnomaly input)
    {
        var stages = new List<AnalysisStage>();
        
        // Stage 1: Process Sensor Data
        var processedData = await context.CallActivityAsync<string>(
            nameof(ProcessSensorDataActivity),
            input.RawSensorData);
        
        stages.Add(new AnalysisStage(
            "ProcessSensorData", 
            input.RawSensorData, 
            processedData, 
            true));
        
        // Gate check: Ensure sensor data processing was successful
        if (string.IsNullOrEmpty(processedData))
        {
            return FailedResult(input.AnomalyId, stages, "Sensor data processing failed");
        }
        
        // Stage 2: Classify Anomaly
        var anomalyType = await context.CallActivityAsync<string>(
            nameof(ClassifyAnomalyActivity),
            processedData);
        
        stages.Add(new AnalysisStage(
            "ClassifyAnomaly", 
            processedData, 
            anomalyType, 
            true));
        
        // Stage 3: Scientific Analysis
        var scientificAnalysis = await context.CallActivityAsync<string>(
            nameof(ScientificAnalysisActivity),
            new { ProcessedData = processedData, AnomalyType = anomalyType });
        
        stages.Add(new AnalysisStage(
            "ScientificAnalysis", 
            anomalyType, 
            scientificAnalysis, 
            true));
        
        // Stage 4: Risk Assessment
        var riskLevel = await context.CallActivityAsync<string>(
            nameof(RiskAssessmentActivity),
            new { AnomalyType = anomalyType, Analysis = scientificAnalysis });
        
        stages.Add(new AnalysisStage(
            "RiskAssessment", 
            scientificAnalysis, 
            riskLevel, 
            true));
        
        // Gate check: Alert bridge if critical risk detected
        if (riskLevel.Contains("CRITICAL", StringComparison.OrdinalIgnoreCase))
        {
            await context.CallActivityAsync(
                nameof(AlertBridgeActivity),
                input.AnomalyId);
        }
        
        // Stage 5: Generate Tactical Recommendation
        var recommendation = await context.CallActivityAsync<string>(
            nameof(GenerateRecommendationActivity),
            new { 
                AnomalyType = anomalyType, 
                Analysis = scientificAnalysis,
                RiskLevel = riskLevel 
            });
        
        stages.Add(new AnalysisStage(
            "GenerateRecommendation", 
            riskLevel, 
            recommendation, 
            true));
        
        return new AnalysisResult(
            input.AnomalyId,
            stages,
            anomalyType,
            scientificAnalysis,
            riskLevel,
            recommendation
        );
    }

    private static AnalysisResult FailedResult(string anomalyId, List<AnalysisStage> stages, string errorMessage)
    {
        return new AnalysisResult(
            anomalyId,
            stages,
            "UNKNOWN",
            errorMessage,
            "UNKNOWN",
            "Analysis failed - manual review required"
        );
    }
}
