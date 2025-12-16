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
            nameof(ProcessSensorDataActivity), 
            input.RawSensorData,
            processedData,
            true));
        context.SetCustomStatus(stages);
        
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
            nameof(ClassifyAnomalyActivity), 
            processedData, 
            anomalyType, 
            true));
        context.SetCustomStatus(stages);
        
        // Stage 3: Scientific Analysis
        var scientificAnalysis = await context.CallActivityAsync<string>(
            nameof(ScientificAnalysisActivity),
            new ScientificAnalysisInput(processedData, anomalyType));
        
        stages.Add(new AnalysisStage(
            nameof(ScientificAnalysisActivity),
            anomalyType,
            scientificAnalysis,
            true));
        context.SetCustomStatus(stages);
        
        // Stage 4: Risk Assessment
        var riskLevel = await context.CallActivityAsync<string>(
            nameof(RiskAssessmentActivity),
            new RiskAssessmentInput(anomalyType, scientificAnalysis));
        
        stages.Add(new AnalysisStage(
            nameof(RiskAssessmentActivity),
            scientificAnalysis,
            riskLevel,
            true));
        context.SetCustomStatus(stages);
        
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
            new GenerateRecommendationInput(
                anomalyType,
                scientificAnalysis,
                riskLevel
            ));
        
        stages.Add(new AnalysisStage(
            nameof(GenerateRecommendationActivity),
            riskLevel,
            recommendation,
            true));
        context.SetCustomStatus(stages);
        
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
