using Dapr.Workflow;
using Microsoft.Extensions.Logging;

namespace AnomalyAnalysis.Activities;

public class AlertBridgeActivity : WorkflowActivity<string, string>
{
    private readonly ILogger<AlertBridgeActivity> _logger;
    
    public AlertBridgeActivity(ILogger<AlertBridgeActivity> logger)
    {
        _logger = logger;
    }
    
    public override Task<string> RunAsync(WorkflowActivityContext context, string anomalyId)
    {
        _logger.LogCritical("🚨 RED ALERT: Critical anomaly detected - {AnomalyId}", anomalyId);
        _logger.LogCritical("Alert sent to Bridge - Captain Picard has been notified");
        
        // In a real system, this would trigger actual alerts (email, SMS, bridge console alerts)
        return Task.FromResult($"Bridge alert sent for anomaly {anomalyId}");
    }
}
