# Demo: Starship Diagnostics - Parallelization Pattern

```mermaid
%%{init: {"theme":"base","themeVariables":{"background":"#0a0a0a","primaryColor":"#1f3a32","primaryTextColor":"#e6e6e6","primaryBorderColor":"#41BD9B","lineColor":"#A6A6A6","secondaryColor":"#111315","tertiaryColor":"#111315","clusterBkg":"#111315","clusterBorder":"#41BD9B","titleColor":"#F0C75E","edgeLabelBackground":"#0a0a0a","noteBkgColor":"#111315","noteTextColor":"#e6e6e6","noteBorderColor":"#41BD9B","actorBkg":"#1f3a32","actorBorder":"#41BD9B","actorTextColor":"#e6e6e6","actorLineColor":"#A6A6A6","signalColor":"#A6A6A6","signalTextColor":"#e6e6e6","labelBoxBkgColor":"#0a0a0a","labelBoxBorderColor":"#41BD9B","labelTextColor":"#e6e6e6","loopTextColor":"#e6e6e6","altSectionBkgColor":"#111315","sectionBkgColor":"#111315","fontFamily":"Geist,sans-serif"}}}%%
graph TD
    Start([Workflow Start: Starship]) --> Parallel{Parallel Scans}
    Parallel -->|Parallel| S1[HullIntegrityScanActivity]
    Parallel -->|Parallel| S2[ReactorCoreScanActivity]
    Parallel -->|Parallel| S3[NavigationScanActivity]
    Parallel -->|Parallel| S4[LifeSupportScanActivity]
    Parallel -->|Parallel| S5[WeaponsScanActivity]
    S1 --> Gather[Gather Results]
    S2 --> Gather
    S3 --> Gather
    S4 --> Gather
    S5 --> Gather
    Gather --> Gate{Critical Findings?}
    Gate -->|Yes| Vote{Parallel Voting}
    Gate -->|No| Aggregate[AggregateResultsActivity]
    Vote -->|Parallel| V1[SafetyVoterActivity]
    Vote -->|Parallel| V2[SeverityVoterActivity]
    Vote -->|Parallel| V3[RecommendationVoterActivity]
    V1 --> GatherVotes[Gather Votes]
    V2 --> GatherVotes
    V3 --> GatherVotes
    GatherVotes --> Aggregate
    Aggregate --> End([Return DiagnosticReport])
    
    style Start fill:#41BD9B,stroke:#2d8a70,color:#0a0a0a
    style End fill:#41BD9B,stroke:#2d8a70,color:#0a0a0a
    style Parallel fill:#F0C75E,stroke:#b8943f,color:#0a0a0a
    style Gate fill:#F0C75E,stroke:#b8943f,color:#0a0a0a
    style Vote fill:#F0C75E,stroke:#b8943f,color:#0a0a0a
    style S1 fill:#1f3a32,stroke:#41BD9B,color:#e6e6e6
    style S2 fill:#1f3a32,stroke:#41BD9B,color:#e6e6e6
    style S3 fill:#1f3a32,stroke:#41BD9B,color:#e6e6e6
    style S4 fill:#1f3a32,stroke:#41BD9B,color:#e6e6e6
    style S5 fill:#1f3a32,stroke:#41BD9B,color:#e6e6e6
    style V1 fill:#E89146,stroke:#a85f1f,color:#0a0a0a
    style V2 fill:#E89146,stroke:#a85f1f,color:#0a0a0a
    style V3 fill:#E89146,stroke:#a85f1f,color:#0a0a0a
```