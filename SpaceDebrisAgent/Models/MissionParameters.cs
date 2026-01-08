namespace SpaceDebrisAgent.Models;

public record MissionParameters(
    string MissionId,
    OrbitalZone OrbitalZone,
    int MaxDebrisPieces,
    double FuelBudget, // kg
    int MaxMissionHours,
    bool RequireHumanApproval, // for high-risk maneuvers
    int StepNumber = 0 // Current step in the workflow
);

public record DebrisField(
    List<DebrisObject> Debris,
    double TotalMass,
    string RiskLevel
);

public record DebrisObject(
    string Id,
    double Mass, // kg
    DebrisType Type,
    double[] Position, // [x, y, z] km
    double[] Velocity, // [vx, vy, vz] km/s
    ThreatLevel ThreatLevel,
    bool IsFragmented
);

public record AgentState(
    MissionPhase CurrentPhase,
    double[] Position, // Current position
    double FuelRemaining,
    List<string> CapturedDebris,
    List<string> DecisionHistory,
    int StepCount,
    Dictionary<string, object> Memory // Agent's working memory
);

public record AgentDecision(
    int StepNumber,
    string Reasoning,
    string ChosenAction,
    Dictionary<string, object> ActionParameters,
    string ExpectedOutcome,
    DateTime Timestamp
);

public record ToolCall(
    string ToolName,
    Dictionary<string, object> Parameters,
    object Result,
    bool Success,
    string? ErrorMessage
);

public record HumanApproval(
    bool Approved,
    string Reason
);

public record MissionResult(
    string MissionId,
    bool Success,
    int DebrisCaptured,
    double FuelUsed,
    int TotalSteps,
    List<AgentDecision> Decisions,
    List<ToolCall> ToolCalls,
    string Summary,
    List<string> LessonsLearned
);

public record ReasoningInput(
    MissionParameters Mission,
    AgentState CurrentState,
    List<ToolCall> PreviousToolCalls
);

public record SpaceDebrisCleanupWorkflowInput(
    MissionParameters MissionParameters,
    AgentState? AgentState,
    List<AgentDecision> Decisions,
    List<ToolCall> ToolCalls
)
{
    // Constructor for initial workflow start
    public SpaceDebrisCleanupWorkflowInput(MissionParameters missionParameters)
        : this(missionParameters, null, new List<AgentDecision>(), new List<ToolCall>())
    {
    }
}

public record CaptureResult(
    string DebrisId,
    bool Success,
    double FuelUsed,
    string Message
);

public record NavigationResult(
    double[] NewPosition,
    double FuelUsed,
    int TimeSeconds,
    bool Success
);

public record ApprovalResult(
    bool Approved,
    string HumanResponse,
    DateTime ResponseTime
);

public record DebrisAnalysis(
    string DebrisId,
    string DetailedComposition,
    double StructuralIntegrity,
    string CaptureRecommendation
);

public record FuelStatus(
    double CurrentFuel,
    double PercentRemaining,
    double EstimatedRange,
    string Warning
);
