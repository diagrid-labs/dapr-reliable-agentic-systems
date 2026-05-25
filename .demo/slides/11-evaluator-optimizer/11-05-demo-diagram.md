# Alien Translator - Evaluator-Optimizer Pattern

```mermaid
%%{init: {"theme":"base","themeVariables":{"background":"#0a0a0a","primaryColor":"#1f3a32","primaryTextColor":"#e6e6e6","primaryBorderColor":"#41BD9B","lineColor":"#A6A6A6","secondaryColor":"#111315","tertiaryColor":"#111315","clusterBkg":"#111315","clusterBorder":"#41BD9B","titleColor":"#F0C75E","edgeLabelBackground":"#0a0a0a","noteBkgColor":"#111315","noteTextColor":"#e6e6e6","noteBorderColor":"#41BD9B","actorBkg":"#1f3a32","actorBorder":"#41BD9B","actorTextColor":"#e6e6e6","actorLineColor":"#A6A6A6","signalColor":"#A6A6A6","signalTextColor":"#e6e6e6","labelBoxBkgColor":"#0a0a0a","labelBoxBorderColor":"#41BD9B","labelTextColor":"#e6e6e6","loopTextColor":"#e6e6e6","altSectionBkgColor":"#111315","sectionBkgColor":"#111315","fontFamily":"Geist,sans-serif"}}}%%
graph TD
    Start([Workflow Start: AlienText]) --> Check1{First Iteration?}
    Check1 -->|Yes| A1[TranslateActivity]
    Check1 -->|No| A3[RefineTranslationActivity]
    A1 --> A2[EvaluateTranslationActivity]
    A3 --> A2
    A2 --> Gate1{Quality ≥ 8.0 AND<br/>Meets Standards?}
    Gate1 -->|Yes| Success([Return Success Result])
    Gate1 -->|No| Gate2{Max Iterations<br/>Reached?}
    Gate2 -->|Yes| MaxReached([Return Best Effort Result])
    Gate2 -->|No| Restart[ContinueAsNew<br/>Restart Workflow]
    Restart -.-> Check1
    
    style Start fill:#41BD9B,stroke:#2d8a70,color:#0a0a0a
    style Success fill:#41BD9B,stroke:#2d8a70,color:#0a0a0a
    style MaxReached fill:#F0C75E,stroke:#b8943f,color:#0a0a0a
    style Gate1 fill:#F0C75E,stroke:#b8943f,color:#0a0a0a
    style Gate2 fill:#F0C75E,stroke:#b8943f,color:#0a0a0a
    style Restart fill:#41BD9B,stroke:#2d8a70,color:#0a0a0a
```