namespace SpaceDebrisAgent.Models;

public enum OrbitalZone
{
    LEO,  // Low Earth Orbit
    MEO,  // Medium Earth Orbit
    GEO   // Geostationary Earth Orbit
}

public enum DebrisType
{
    Satellite,
    RocketStage,
    Fragment
}

public enum ThreatLevel
{
    Low,
    Medium,
    High,
    Critical
}

public enum MissionPhase
{
    Planning,
    Executing,
    Monitoring,
    Complete
}
