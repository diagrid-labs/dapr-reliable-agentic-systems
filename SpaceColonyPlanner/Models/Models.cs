namespace SpaceColonyPlanner.Models;

public record Planet(
    string PlanetId,
    string Name,
    PlanetaryConditions Conditions,
    AvailableResources Resources
);

public record PlanetaryConditions(
    double Gravity, // Earth = 1.0
    AtmosphereType AtmosphereType,
    double Temperature, // Celsius
    double RadiationLevel, // Sv/year
    bool HasWater,
    double DayLength // Earth days
);

public record AvailableResources(
    bool Metals,
    bool RareEarths,
    bool Water,
    bool Organics,
    bool Uranium,
    SoilQuality SoilQuality
);

public record ColonyRequirements(
    int InitialPopulation,
    int TargetPopulation,
    Purpose Purpose,
    int YearsToComplete
);

public record StructureRequest(
    string StructureType,
    Priority Priority,
    int Quantity,
    string Reasoning
);

public record StructurePlan(
    string StructureType,
    int Quantity,
    List<string> Materials,
    int ConstructionDays,
    int WorkerHours,
    List<string> Prerequisites,
    string DetailedSpecification
);

public record ColonyMasterPlan(
    string PlanetId,
    List<StructurePlan> Structures,
    int TotalConstructionDays,
    Dictionary<string, int> MaterialsRequired,
    List<ConstructionPhase> Timeline,
    string SuccessFactors,
    string RiskAssessment
);

public record ConstructionPhase(
    int PhaseNumber,
    string Name,
    List<string> Structures,
    int DurationDays
);

public record ColonyRequest(Planet Planet, ColonyRequirements Requirements);

public record WorkerInput(StructureRequest Request, Planet Planet, PlanetAnalysis Analysis);

public record PlanetAnalysis(
    List<string> Challenges,
    List<string> Opportunities,
    string RecommendedApproach
);

public record DetermineStructuresInput(
    Planet Planet,
    ColonyRequirements Requirements,
    PlanetAnalysis PlanetAnalysis
);

public record SynthesizePlanInput(
    string PlanetId,
    List<StructurePlan> StructurePlans
);
