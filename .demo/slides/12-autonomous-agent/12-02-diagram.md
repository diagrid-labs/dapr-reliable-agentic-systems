# Autonomous Agent Diagram

```mermaid
graph TD
    Start([User Input/Goal]) --> A1[Reasoning & Planning LLM]
    A1 --> Gate{Decision}
    Gate -->|Use Tool| A3[Select & Execute Tool]
    Gate -->|Goal Achieved| End([Complete])
    Gate -->|Max Steps| End
    A3 --> A4[Observe Result]
    A4 --> A5[Update State]
    A5 --> A1
    
    style Start fill:#e1f5ff
    style Gate fill:#fff3cd
    style A3 fill:#cfe2ff
    style End fill:#d4edda
```

### Agent Loop

Perceive → Reason → Act → Observe → Repeat
