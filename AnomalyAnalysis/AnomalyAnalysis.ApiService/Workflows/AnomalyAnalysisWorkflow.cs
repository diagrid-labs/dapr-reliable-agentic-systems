using Dapr.Workflow;
using AnomalyAnalysis.Models;
using AnomalyAnalysis.Activities;

namespace AnomalyAnalysis.Workflows;

public class AnomalyAnalysisWorkflow : Workflow<SpatialAnomaly, AnalysisResult>
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
        SpatialAnomaly input)
    {
        var stages = new List<AnalysisStage>();
        var usage = TokenUsage.Zero;
        
        // Stage 1: Process Sensor Data
        var processedDataResult = await context.CallActivityAsync<Metered<string>>(
            nameof(ProcessSensorDataActivity),
            input.RawSensorData,
            GetDefaultRetryPolicy());
        var processedData = processedDataResult.Value;
        usage += processedDataResult.Usage;
        
        stages.Add(new AnalysisStage(
            nameof(ProcessSensorDataActivity), 
            input.RawSensorData,
            processedData,
            true));
        context.SetCustomStatus(new AnalysisProgress(stages.ToList(), usage));
        
        // Gate check: Ensure sensor data processing was successful
        if (string.IsNullOrEmpty(processedData))
        {
            return FailedResult(input.AnomalyId, stages, "Sensor data processing failed");
        }
        
        // Stage 2: Classify Anomaly
        var anomalyTypeResult = await context.CallActivityAsync<Metered<string>>(
            nameof(ClassifyAnomalyActivity),
            processedData,
            GetDefaultRetryPolicy());
        var anomalyType = anomalyTypeResult.Value;
        usage += anomalyTypeResult.Usage;
        
        stages.Add(new AnalysisStage(
            nameof(ClassifyAnomalyActivity), 
            processedData, 
            anomalyType, 
            true));
        context.SetCustomStatus(new AnalysisProgress(stages.ToList(), usage));
        
        // Stage 3: Scientific Analysis
        var scientificAnalysisResult = await context.CallActivityAsync<Metered<string>>(
            nameof(ScientificAnalysisActivity),
            new ScientificAnalysisInput(processedData, anomalyType),
            GetDefaultRetryPolicy());
        var scientificAnalysis = scientificAnalysisResult.Value;
        usage += scientificAnalysisResult.Usage;
        
        stages.Add(new AnalysisStage(
            nameof(ScientificAnalysisActivity),
            anomalyType,
            scientificAnalysis,
            true));
        context.SetCustomStatus(new AnalysisProgress(stages.ToList(), usage));
        
        // Stage 4: Risk Assessment
        var riskLevelResult = await context.CallActivityAsync<Metered<string>>(
            nameof(RiskAssessmentActivity),
            new RiskAssessmentInput(anomalyType, scientificAnalysis),
            GetDefaultRetryPolicy());
        var riskLevel = riskLevelResult.Value;
        usage += riskLevelResult.Usage;
        
        stages.Add(new AnalysisStage(
            nameof(RiskAssessmentActivity),
            scientificAnalysis,
            riskLevel,
            true));
        context.SetCustomStatus(new AnalysisProgress(stages.ToList(), usage));
        
        // Gate check: Alert bridge if critical risk detected
        if (riskLevel.Contains("CRITICAL", StringComparison.OrdinalIgnoreCase))
        {
            await context.CallActivityAsync(
                nameof(AlertBridgeActivity),
                input.AnomalyId,
                GetDefaultRetryPolicy());
        }
        
        // Stage 5: Generate Tactical Recommendation
        var recommendationResult = await context.CallActivityAsync<Metered<string>>(
            nameof(GenerateRecommendationActivity),
            new GenerateRecommendationInput(
                anomalyType,
                scientificAnalysis,
                riskLevel
            ),
            GetDefaultRetryPolicy());
        var recommendation = recommendationResult.Value;
        usage += recommendationResult.Usage;
        
        stages.Add(new AnalysisStage(
            nameof(GenerateRecommendationActivity),
            riskLevel,
            recommendation,
            true));
        context.SetCustomStatus(new AnalysisProgress(stages.ToList(), usage));
        
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
