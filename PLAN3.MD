# Plan 3: Workflow Parallelization - Starship Diagnostic Multi-Scanner

## Overview
This demo implements a **parallelization workflow** using Dapr Workflow to perform comprehensive diagnostics on the USS Enterprise-D. Geordi La Forge's system runs multiple independent scans simultaneously (sectioning) across all major ship systems, and uses voting mechanisms where Data, Worf, and Dr. Crusher independently assess critical findings before reporting to Captain Picard.

## Sci-Fi Use Case: Commander La Forge's Enterprise-D Diagnostic System
You're building a backend system for Geordi La Forge to perform comprehensive diagnostics on the USS Enterprise-D. After a challenging mission through the Typhon Expanse, Geordi needs to run parallel diagnostic scans across all major systems before Captain Picard can authorize the next mission:
- **Hull Integrity Scanner** - Checks for micrometeorite damage, structural stress fractures
- **Warp Core Analyzer** - Evaluates matter/antimatter reaction chamber and dilithium crystal alignment
- **Navigation System Validator** - Tests warp drive calibration and navigational deflector accuracy
- **Life Support Inspector** - Dr. Crusher's protocols verify oxygen recyclers, water purifiers, artificial gravity
- **Tactical System Auditor** - Worf's security protocols check phaser arrays, photon torpedoes, shield generators

Additionally, for critical safety findings (warp core instability, hull breaches), Geordi consults Data, Worf, and Dr. Crusher who independently vote on severity before reporting to Captain Picard.

## Architecture

### Services
1. **StarshipDiagnosticsAPI** - ASP.NET Core Web API (Entry point)
2. **ParallelDiagnosticsWorkflow** - Dapr Workflow orchestrator
3. **ScannerActivities** - Independent diagnostic activities that run in parallel
4. **VoterActivities** - Multiple evaluation activities for critical findings

### Components Used
- **Dapr Workflow** - Orchestrates parallel execution and voting
- **Dapr State Management** - Stores scan results and maintenance history
- **Dapr Conversation API** - Powers each diagnostic scan and vote

## Detailed Implementation Plan

### Step 1: Project Setup
```
/StarshipDiagnostics
  - Program.cs
  - local.http
  - appsettings.json
  - dapr.yaml
  /Workflows
    - ParallelDiagnosticsWorkflow.cs
  /Activities
    /Scanners
      - HullIntegrityScanActivity.cs
      - ReactorCoreScanActivity.cs
      - NavigationScanActivity.cs
      - LifeSupportScanActivity.cs
      - WeaponsScanActivity.cs
    /Voters
      - SafetyVoterActivity.cs
      - SeverityVoterActivity.cs
      - RecommendationVoterActivity.cs
    - AggregateResultsActivity.cs
  /Models
    - Starship.cs
    - DiagnosticReport.cs
    - ScanResult.cs
    - VoteResult.cs
  /Resources
    - statestore.yaml
    - conversation.yaml
```

### Step 2: Core Models
**Starship.cs**
```csharp
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
    string Status, // OK, WARNING, CRITICAL
    double HealthPercentage,
    List<string> Issues,
    List<string> Recommendations,
    string DetailedAnalysis
);

public record VoteResult(
    string Category,
    Dictionary<string, string> Votes, // voterId -> vote
    string Consensus,
    double AgreementLevel
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
```

### Step 3: Workflow Definition with Parallelization
**ParallelDiagnosticsWorkflow.cs**
```csharp
public class ParallelDiagnosticsWorkflow : Workflow<Starship, DiagnosticReport>
{
    public override async Task<DiagnosticReport> RunAsync(
        WorkflowContext context, 
        Starship input)
    {
        // SECTIONING: Run independent scans in parallel
        var scanTasks = new List<Task<ScanResult>>
        {
            context.CallActivityAsync<ScanResult>(
                nameof(HullIntegrityScanActivity),
                input),
            context.CallActivityAsync<ScanResult>(
                nameof(ReactorCoreScanActivity),
                input),
            context.CallActivityAsync<ScanResult>(
                nameof(NavigationScanActivity),
                input),
            context.CallActivityAsync<ScanResult>(
                nameof(LifeSupportScanActivity),
                input),
            context.CallActivityAsync<ScanResult>(
                nameof(WeaponsScanActivity),
                input)
        };
        
        // Wait for all scans to complete
        var scanResults = await Task.WhenAll(scanTasks);
        
        // Identify critical findings that need voting
        var criticalFindings = scanResults
            .Where(r => r.Status == "CRITICAL")
            .ToList();
        
        var voteResults = new List<VoteResult>();
        
        if (criticalFindings.Any())
        {
            // VOTING: Multiple AI models vote on critical findings
            // Run votes in parallel for each critical finding
            foreach (var finding in criticalFindings)
            {
                // Parallel voting - 3 different AI perspectives
                var voteTasks = new List<Task<KeyValuePair<string, string>>>
                {
                    context.CallActivityAsync<KeyValuePair<string, string>>(
                        nameof(SafetyVoterActivity),
                        finding),
                    context.CallActivityAsync<KeyValuePair<string, string>>(
                        nameof(SeverityVoterActivity),
                        finding),
                    context.CallActivityAsync<KeyValuePair<string, string>>(
                        nameof(RecommendationVoterActivity),
                        finding)
                };
                
                var votes = await Task.WhenAll(voteTasks);
                
                // Aggregate votes
                var voteDict = votes.ToDictionary(v => v.Key, v => v.Value);
                var consensus = DetermineConsensus(voteDict);
                var agreement = CalculateAgreement(voteDict);
                
                voteResults.Add(new VoteResult(
                    finding.SubsystemName,
                    voteDict,
                    consensus,
                    agreement
                ));
            }
        }
        
        // Aggregate all results
        var aggregationInput = new { ScanResults = scanResults, VoteResults = voteResults };
        var finalReport = await context.CallActivityAsync<DiagnosticReport>(
            nameof(AggregateResultsActivity),
            aggregationInput);
        
        return finalReport;
    }
    
    private string DetermineConsensus(Dictionary<string, string> votes)
    {
        // Majority voting
        var groups = votes.GroupBy(v => v.Value)
            .OrderByDescending(g => g.Count())
            .ToList();
        
        return groups.First().Key;
    }
    
    private double CalculateAgreement(Dictionary<string, string> votes)
    {
        var groups = votes.GroupBy(v => v.Value);
        var maxCount = groups.Max(g => g.Count());
        return (double)maxCount / votes.Count;
    }
}
```

### Step 4: Scanner Activities (Parallel Sectioning)

> **Note**: All scanner and voter activities use `DaprConversationClient` with constructor injection for LLM calls. See PLAN1 for the complete pattern with `ConversationOptions`, `SystemMessage`, `UserMessage`, and `ConverseAsync()`.

**HullIntegrityScanActivity.cs**
```csharp
public class HullIntegrityScanActivity : WorkflowActivity<Starship, ScanResult>
{
    private readonly DaprClient _daprClient;
    
    public HullIntegrityScanActivity(DaprClient daprClient)
    {
        _daprClient = daprClient;
    }
    
    public override async Task<ScanResult> RunAsync(
        WorkflowActivityContext context, 
        Starship input)
    {
        var telemetryData = JsonSerializer.Serialize(input.Telemetry);
        
        var conversationRequest = new
        {
            model = "gpt-4o-mini", // Use cheaper model for routine scans
            messages = new[]
            {
                new { 
                    role = "system", 
                    content = @"You are a hull integrity scanner AI. Analyze starship hull 
                    condition for:
                    - Micrometeorite impacts
                    - Stress fractures from FTL travel
                    - Corrosion from space radiation
                    - Structural weak points
                    
                    Return JSON: {
                      ""status"": ""OK|WARNING|CRITICAL"",
                      ""healthPercentage"": 0-100,
                      ""issues"": [""issue1"", ""issue2""],
                      ""recommendations"": [""rec1"", ""rec2""],
                      ""detailedAnalysis"": ""full analysis text""
                    }" 
                },
                new { 
                    role = "user", 
                    content = $@"Ship: {input.Name} ({input.Class})
Years in service: {input.YearsInService}
Last maintenance: {input.LastMaintenance}
Telemetry: {telemetryData}

Scan hull integrity:" 
                }
            },
            response_format = new { type = "json_object" }
        };
        
        var response = await _daprClient.InvokeMethodAsync<object, ConversationResponse>(
            HttpMethod.Post,
            "conversation",
            "v1/chat/completions",
            conversationRequest);
        
        return ParseScanResult("Hull Integrity", response.Choices[0].Message.Content);
    }
    
    private ScanResult ParseScanResult(string subsystem, string jsonContent)
    {
        var json = JsonSerializer.Deserialize<JsonElement>(jsonContent);
        return new ScanResult(
            subsystem,
            json.GetProperty("status").GetString(),
            json.GetProperty("healthPercentage").GetDouble(),
            JsonSerializer.Deserialize<List<string>>(
                json.GetProperty("issues").GetRawText()),
            JsonSerializer.Deserialize<List<string>>(
                json.GetProperty("recommendations").GetRawText()),
            json.GetProperty("detailedAnalysis").GetString()
        );
    }
}
```

**ReactorCoreScanActivity.cs**
```csharp
public class ReactorCoreScanActivity : WorkflowActivity<Starship, ScanResult>
{
    private readonly DaprClient _daprClient;
    
    public override async Task<ScanResult> RunAsync(
        WorkflowActivityContext context, 
        Starship input)
    {
        var telemetryData = JsonSerializer.Serialize(input.Telemetry);
        
        var conversationRequest = new
        {
            model = "gpt-4o", // Use powerful model for critical reactor analysis
            messages = new[]
            {
                new { 
                    role = "system", 
                    content = @"You are a fusion reactor specialist AI. Analyze reactor core for:
                    - Containment field stability
                    - Plasma temperature regulation
                    - Radiation shielding integrity
                    - Fuel efficiency and burn rate
                    - Coolant system performance
                    
                    This is CRITICAL - reactor failures are catastrophic.
                    
                    Return JSON with status, healthPercentage, issues, recommendations, 
                    and detailedAnalysis." 
                },
                new { 
                    role = "user", 
                    content = $@"Ship: {input.Name}
Years in service: {input.YearsInService}
Telemetry: {telemetryData}

Scan reactor core:" 
                }
            },
            response_format = new { type = "json_object" }
        };
        
        var response = await _daprClient.InvokeMethodAsync<object, ConversationResponse>(
            HttpMethod.Post,
            "conversation",
            "v1/chat/completions",
            conversationRequest);
        
        return ParseScanResult("Reactor Core", response.Choices[0].Message.Content);
    }
}
```

**LifeSupportScanActivity.cs**
```csharp
public class LifeSupportScanActivity : WorkflowActivity<Starship, ScanResult>
{
    private readonly DaprClient _daprClient;
    
    public override async Task<ScanResult> RunAsync(
        WorkflowActivityContext context, 
        Starship input)
    {
        var conversationRequest = new
        {
            model = "gpt-4o-mini",
            messages = new[]
            {
                new { 
                    role = "system", 
                    content = @"You are a life support systems expert. Analyze:
                    - Oxygen generation and recycling
                    - Water purification systems
                    - Artificial gravity generators
                    - Temperature and humidity control
                    - Air quality and contamination levels
                    
                    Return JSON with status, healthPercentage, issues, recommendations, 
                    and detailedAnalysis." 
                },
                new { 
                    role = "user", 
                    content = $"Ship: {input.Name}, Years: {input.YearsInService}\n\nScan life support:" 
                }
            },
            response_format = new { type = "json_object" }
        };
        
        var response = await _daprClient.InvokeMethodAsync<object, ConversationResponse>(
            HttpMethod.Post,
            "conversation",
            "v1/chat/completions",
            conversationRequest);
        
        return ParseScanResult("Life Support", response.Choices[0].Message.Content);
    }
}
```

### Step 5: Voter Activities (Parallel Voting)

**SafetyVoterActivity.cs**
```csharp
public class SafetyVoterActivity : WorkflowActivity<ScanResult, KeyValuePair<string, string>>
{
    private readonly DaprClient _daprClient;
    
    public override async Task<KeyValuePair<string, string>> RunAsync(
        WorkflowActivityContext context, 
        ScanResult input)
    {
        var conversationRequest = new
        {
            model = "gpt-4o",
            messages = new[]
            {
                new { 
                    role = "system", 
                    content = @"You are a SAFETY-FOCUSED evaluator. Analyze the finding from 
                    a crew safety perspective. Is this finding:
                    - IMMEDIATE_GROUNDING: Ship cannot safely operate
                    - URGENT_REPAIR: Must fix before next voyage
                    - SCHEDULED_MAINTENANCE: Can wait for next scheduled service
                    
                    Return only one of these three values." 
                },
                new { 
                    role = "user", 
                    content = $@"Subsystem: {input.SubsystemName}
Status: {input.Status}
Health: {input.HealthPercentage}%
Issues: {string.Join(", ", input.Issues)}

Vote on safety classification:" 
                }
            }
        };
        
        var response = await _daprClient.InvokeMethodAsync<object, ConversationResponse>(
            HttpMethod.Post,
            "conversation",
            "v1/chat/completions",
            conversationRequest);
        
        return new KeyValuePair<string, string>(
            "SafetyVoter", 
            response.Choices[0].Message.Content.Trim());
    }
}
```

**SeverityVoterActivity.cs**
```csharp
public class SeverityVoterActivity : WorkflowActivity<ScanResult, KeyValuePair<string, string>>
{
    private readonly DaprClient _daprClient;
    
    public override async Task<KeyValuePair<string, string>> RunAsync(
        WorkflowActivityContext context, 
        ScanResult input)
    {
        var conversationRequest = new
        {
            model = "gpt-4o",
            messages = new[]
            {
                new { 
                    role = "system", 
                    content = @"You are a SEVERITY-FOCUSED evaluator. Analyze the technical 
                    severity of the finding. Consider:
                    - Likelihood of catastrophic failure
                    - Time to failure if not addressed
                    - Potential for cascading failures
                    
                    Classify as:
                    - IMMEDIATE_GROUNDING
                    - URGENT_REPAIR
                    - SCHEDULED_MAINTENANCE" 
                },
                new { 
                    role = "user", 
                    content = $@"Subsystem: {input.SubsystemName}
Analysis: {input.DetailedAnalysis}

Vote on severity classification:" 
                }
            }
        };
        
        var response = await _daprClient.InvokeMethodAsync<object, ConversationResponse>(
            HttpMethod.Post,
            "conversation",
            "v1/chat/completions",
            conversationRequest);
        
        return new KeyValuePair<string, string>(
            "SeverityVoter", 
            response.Choices[0].Message.Content.Trim());
    }
}
```

**RecommendationVoterActivity.cs**
```csharp
public class RecommendationVoterActivity : WorkflowActivity<ScanResult, KeyValuePair<string, string>>
{
    private readonly DaprClient _daprClient;
    
    public override async Task<KeyValuePair<string, string>> RunAsync(
        WorkflowActivityContext context, 
        ScanResult input)
    {
        var conversationRequest = new
        {
            model = "gpt-4o",
            messages = new[]
            {
                new { 
                    role = "system", 
                    content = @"You are a COST-BENEFIT evaluator. Balance repair urgency 
                    against operational needs and costs. Consider:
                    - Can the ship operate safely with temporary measures?
                    - Cost of immediate repair vs scheduled maintenance
                    - Mission criticality
                    
                    Classify as:
                    - IMMEDIATE_GROUNDING
                    - URGENT_REPAIR
                    - SCHEDULED_MAINTENANCE" 
                },
                new { 
                    role = "user", 
                    content = $@"Subsystem: {input.SubsystemName}
Recommendations: {string.Join(", ", input.Recommendations)}

Vote on action classification:" 
                }
            }
        };
        
        var response = await _daprClient.InvokeMethodAsync<object, ConversationResponse>(
            HttpMethod.Post,
            "conversation",
            "v1/chat/completions",
            conversationRequest);
        
        return new KeyValuePair<string, string>(
            "RecommendationVoter", 
            response.Choices[0].Message.Content.Trim());
    }
}
```

### Step 6: Aggregation Activity
**AggregateResultsActivity.cs**
```csharp
public class AggregateResultsActivity : WorkflowActivity<object, DiagnosticReport>
{
    public override async Task<DiagnosticReport> RunAsync(
        WorkflowActivityContext context, 
        object input)
    {
        var data = (dynamic)input;
        List<ScanResult> scanResults = data.ScanResults;
        List<VoteResult> voteResults = data.VoteResults;
        
        // Determine overall status
        var hasCritical = scanResults.Any(s => s.Status == "CRITICAL");
        var hasWarning = scanResults.Any(s => s.Status == "WARNING");
        
        string overallStatus = hasCritical ? "CRITICAL" : 
                              hasWarning ? "WARNING" : "OK";
        
        // Check if any votes resulted in IMMEDIATE_GROUNDING consensus
        var mustGround = voteResults.Any(v => 
            v.Consensus == "IMMEDIATE_GROUNDING");
        
        // Collect required repairs
        var requiredRepairs = scanResults
            .Where(s => s.Status != "OK")
            .SelectMany(s => s.Recommendations)
            .Distinct()
            .ToList();
        
        // Estimate repair time
        int estimatedHours = scanResults.Count(s => s.Status == "CRITICAL") * 24 +
                           scanResults.Count(s => s.Status == "WARNING") * 4;
        
        return new DiagnosticReport(
            "SHIP-ID", // Would come from input
            DateTime.UtcNow,
            scanResults.ToList(),
            voteResults.ToList(),
            overallStatus,
            ClearForDeparture: !mustGround && !hasCritical,
            requiredRepairs,
            estimatedHours
        );
    }
}
```

### Step 7: API Endpoints
**Program.cs**
```csharp
using Dapr.Client;
using Dapr.Workflow;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDaprClient();
builder.Services.AddDaprConversationClient();
builder.Services.AddDaprWorkflow(options =>
{
    options.RegisterWorkflow<ParallelDiagnosticsWorkflow>();
    
    // Register scanner activities
    options.RegisterActivity<HullIntegrityScanActivity>();
    options.RegisterActivity<ReactorCoreScanActivity>();
    options.RegisterActivity<NavigationScanActivity>();
    options.RegisterActivity<LifeSupportScanActivity>();
    options.RegisterActivity<WeaponsScanActivity>();
    
    // Register voter activities
    options.RegisterActivity<SafetyVoterActivity>();
    options.RegisterActivity<SeverityVoterActivity>();
    options.RegisterActivity<RecommendationVoterActivity>();
    
    // Register aggregation
    options.RegisterActivity<AggregateResultsActivity>();
});

var app = builder.Build();

// Start diagnostic scan
app.MapPost("/ship/diagnose", async (
    Starship ship,
    DaprWorkflowClient workflowClient,
    DaprClient daprClient) =>
{
    var instanceId = $"diagnostic-{ship.ShipId}-{DateTime.UtcNow.Ticks}";
    
    // Store ship data
    await daprClient.SaveStateAsync(
        "statestore", 
        $"ship-{ship.ShipId}", 
        ship);
    
    // Start parallel diagnostics workflow
    await workflowClient.ScheduleNewWorkflowAsync(
        nameof(ParallelDiagnosticsWorkflow),
        instanceId,
        ship);
    
    return Results.Accepted($"/ship/report/{instanceId}", new { instanceId });
});

// Get diagnostic report
app.MapGet("/ship/report/{instanceId}", async (
    string instanceId,
    DaprWorkflowClient workflowClient) =>
{
    var state = await workflowClient.GetWorkflowStateAsync(instanceId);
    
    if (state == null)
        return Results.NotFound();
    
    return Results.Ok(new
    {
        instanceId,
        status = state.RuntimeStatus.ToString(),
        report = state.ReadOutputAs<DiagnosticReport>(),
        createdAt = state.CreatedAt
    });
});

// Get ship maintenance history
app.MapGet("/ship/{shipId}/history", async (
    string shipId,
    DaprClient daprClient) =>
{
    // Query all diagnostics for this ship
    var shipData = await daprClient.GetStateAsync<Starship>(
        "statestore",
        $"ship-{shipId}");
    
    return Results.Ok(new
    {
        ship = shipData,
        lastScan = DateTime.UtcNow.AddDays(-3) // Mock data
    });
});

app.Run();
```

### Step 8: Dapr MultiApp Run
**dapr.yaml**
```yaml
version: 1
common:
  resourcesPath: Resources
  appLogDestination: fileAndConsole
  daprdLogDestination: fileAndConsole
apps:
  - appID: starship-diagnostics-app
    appDirPath: /
    appPort: 5500
    daprHTTPPort: 3550
    command: ["dotnet", "run"]
```

### Step 9: Local HTTP Tests
**local.http**
```http
### Diagnose a starship with multiple issues
POST http://localhost:5000/ship/diagnose
Content-Type: application/json

{
  "shipId": "NCC-1701-D",
  "name": "USS Enterprise",
  "class": "Galaxy",
  "yearsInService": 15,
  "lastMaintenance": "2424-12-01T00:00:00Z",
  "telemetry": {
    "hullStressLevel": 0.67,
    "reactorTemp": 15000000,
    "reactorContainment": 0.94,
    "oxygenLevel": 0.98,
    "navigationAccuracy": 0.999,
    "shieldCapacity": 0.85
  }
}

### Diagnose an aging ship
POST http://localhost:5000/ship/diagnose
Content-Type: application/json

{
  "shipId": "NCC-1701",
  "name": "USS Enterprise (Original)",
  "class": "Constitution",
  "yearsInService": 40,
  "lastMaintenance": "2420-06-15T00:00:00Z",
  "telemetry": {
    "hullStressLevel": 0.89,
    "reactorTemp": 16500000,
    "reactorContainment": 0.78,
    "oxygenLevel": 0.91,
    "navigationAccuracy": 0.95,
    "shieldCapacity": 0.62
  }
}

### Get diagnostic report
GET http://localhost:5000/ship/report/diagnostic-NCC-1701-D-638742234567890000

### Get ship history
GET http://localhost:5000/ship/NCC-1701-D/history
```

## Benefits of Parallelization Pattern

### 1. **Massive Speed Improvements**
- **Sequential**: 5 scans × 3 seconds each = 15 seconds
- **Parallel**: max(scan times) = ~3 seconds
- **5x faster** for this workload

### 2. **Better Accuracy Through Voting**
Multiple AI perspectives on critical findings:
- Reduces false positives (all three voters must agree for high-confidence decisions)
- Catches different aspects (safety vs severity vs cost-benefit)
- Provides consensus confidence level
- More reliable than single AI judgment

### 3. **Independent Specialization**
Each scanner can:
- Use different models (cheap for hull, expensive for reactor)
- Have specialized prompts for its domain
- Tune temperature/parameters independently
- Evolve without affecting other scanners

### 4. **Efficient Resource Usage**
- Parallel execution maximizes throughput
- Can use smaller/cheaper models for simple scans
- Critical scans get powerful models
- Voting only happens when needed (critical findings)

### 5. **Fault Isolation**
- One scanner failure doesn't block others
- Partial results still valuable
- Can retry failed scans without re-running successful ones
- Graceful degradation

### 6. **Easy to Scale**
- Add new subsystem scans without modifying existing ones
- Add more voters for higher confidence
- Can run on distributed infrastructure
- Embarrassingly parallel workload

## Drawbacks of Parallelization Pattern

### 1. **Coordination Complexity**
- Must wait for ALL tasks to complete (`Task.WhenAll`)
- Slowest task determines total latency
- One slow/stuck task blocks final result
- Harder to debug than sequential flow

### 2. **Resource Burst**
- All LLM calls fire simultaneously
- Can hit rate limits
- High instantaneous cost
- May need throttling or batching

### 3. **Result Aggregation Logic**
- Must programmatically combine results
- Voting logic can be complex
- Tie-breaking rules needed
- Confidence thresholds require tuning

### 4. **Limited Applicability**
Only works when tasks are truly independent:
- Can't parallelize if task B needs task A's output
- Not suitable for sequential pipelines
- Voting requires comparable/combinable outputs

### 5. **Testing Complexity**
- Must test all combinations of results
- Voting scenarios multiply test cases
- Race conditions possible
- Harder to reproduce issues

### 6. **Overhead for Simple Cases**
- Ship with no issues still runs all 5 scans
- Voting overhead even when consensus is obvious
- Can't short-circuit if early scan shows everything is fine

## When to Use This Pattern

**Use parallelization when:**
- Tasks are independent and can run simultaneously
- Speed is critical
- Multiple perspectives improve accuracy (voting)
- Different aspects need different expertise (sectioning)
- Partial results have value

**Avoid parallelization when:**
- Tasks have dependencies (use chaining)
- Sequential processing is fast enough
- Resource bursts are problematic
- Aggregation logic is too complex

## Running the Demo

### Prerequisites
```powershell
# Install Dapr CLI
powershell -Command "iwr -useb https://raw.githubusercontent.com/dapr/cli/master/install/install.ps1 | iex"

# Initialize Dapr
dapr init

# Run the Diagrid Dashboard
docker run -p 8080:8080 ghcr.io/diagridio/diagrid-dashboard:latest
```

### Run the application
```powershell
# Run with Dapr MultiApp Run
dapr run -f .
```

### Test the workflow
Use the VSCode REST Client extension with `local.http` to diagnose ships and see parallel execution.

## Conclusion
The parallelization workflow pattern excels when you need speed and can run tasks independently. By running diagnostic scans in parallel and using voting for critical findings, the system delivers fast, accurate, and reliable starship diagnostics. Dapr Workflow's built-in support for parallel task execution makes this pattern straightforward to implement while maintaining durability and observability.
