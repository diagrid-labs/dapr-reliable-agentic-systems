using Dapr.Client;
using Dapr.Workflow;

namespace SpaceDebrisAgent.Activities.Tools;

public class RequestHumanApprovalActivity : WorkflowActivity<Dictionary<string, object>, object>
{
    private readonly DaprClient _daprClient;
    
    public RequestHumanApprovalActivity(DaprClient daprClient)
    {
        _daprClient = daprClient;
    }
    
    public override async Task<object> RunAsync(
        WorkflowActivityContext context, 
        Dictionary<string, object> parameters)
    {
        var reason = parameters["reason"].ToString()!;
        
        // Store approval request in state for human operator to view
        await _daprClient.SaveStateAsync(
            "statestore",
            $"approval-request-{context.InstanceId}",
            new { reason, timestamp = DateTime.UtcNow, status = "Pending" });
        
        // Return immediately - workflow will wait for external event
        return new { message = "Approval request stored, waiting for human response" };
    }
}
