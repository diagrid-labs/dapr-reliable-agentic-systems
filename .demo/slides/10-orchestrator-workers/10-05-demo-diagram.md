# Demo: Space Colony Planner - Orchestrator-Worker Pattern

```mermaid
%%{init: {"theme":"base","themeVariables":{"background":"#0a0a0a","primaryColor":"#1f3a32","primaryTextColor":"#e6e6e6","primaryBorderColor":"#41BD9B","lineColor":"#A6A6A6","secondaryColor":"#111315","tertiaryColor":"#111315","clusterBkg":"#111315","clusterBorder":"#41BD9B","titleColor":"#F0C75E","edgeLabelBackground":"#0a0a0a","noteBkgColor":"#111315","noteTextColor":"#e6e6e6","noteBorderColor":"#41BD9B","actorBkg":"#1f3a32","actorBorder":"#41BD9B","actorTextColor":"#e6e6e6","actorLineColor":"#A6A6A6","signalColor":"#A6A6A6","signalTextColor":"#e6e6e6","labelBoxBkgColor":"#0a0a0a","labelBoxBorderColor":"#41BD9B","labelTextColor":"#e6e6e6","loopTextColor":"#e6e6e6","altSectionBkgColor":"#111315","sectionBkgColor":"#111315","fontFamily":"Geist,sans-serif"}}}%%
graph TD
    Start([Workflow Start: ColonyRequest]) --> A1[AnalyzePlanetActivity]
    A1 --> A2[DetermineStructuresActivity]
    A2 --> Router{Dynamic Structure Routing}
    Router -.->|HabitatDome| W1[PlanHabitatDomeActivity]
    Router -.->|PowerPlant| W2[PlanPowerPlantActivity]
    Router -.->|Agriculture| W3[PlanAgricultureActivity]
    Router -.->|MiningFacility| W4[PlanMiningFacilityActivity]
    Router -.->|ResearchLab| W5[PlanResearchLabActivity]
    Router -.->|DefenseSystem| W6[PlanDefenseSystemActivity]
    W1 --> Gather[Task.WhenAll - Gather Worker Results]
    W2 --> Gather
    W3 --> Gather
    W4 --> Gather
    W5 --> Gather
    W6 --> Gather
    Gather --> A3[SynthesizePlanActivity]
    A3 --> A4[OptimizeTimelineActivity]
    A4 --> End([Return ColonyMasterPlan])
    
    style Start fill:#41BD9B,stroke:#2d8a70,color:#0a0a0a
    style End fill:#41BD9B,stroke:#2d8a70,color:#0a0a0a
    style Router fill:#F0C75E,stroke:#b8943f,color:#0a0a0a
    style Gather fill:#F0C75E,stroke:#b8943f,color:#0a0a0a
    style W1 fill:#1f3a32,stroke:#41BD9B,color:#e6e6e6
    style W2 fill:#1f3a32,stroke:#41BD9B,color:#e6e6e6
    style W3 fill:#1f3a32,stroke:#41BD9B,color:#e6e6e6
    style W4 fill:#1f3a32,stroke:#41BD9B,color:#e6e6e6
    style W5 fill:#1f3a32,stroke:#41BD9B,color:#e6e6e6
    style W6 fill:#1f3a32,stroke:#41BD9B,color:#e6e6e6
```