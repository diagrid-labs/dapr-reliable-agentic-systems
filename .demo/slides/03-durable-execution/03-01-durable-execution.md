---
layout: section
---

# Durable Execution

![Animation](.demo/images/bot-animations-2.gif)

---
layout: default
---

# What is Durable Execution?

Durable Execution enables code to run to completion even if the process that runs the code fails. A new process will pick up where the previous one left off.

- **Automatic State Persistence** - Workflow state is automatically checkpointed
- **Replay Mechanism** - Deterministic re-execution

```mermaid
graph LR
    Start([Start]) --> A1[Step 1]
    A1 --> A2[Step 2]
    A2 --> A3[Step 3]
    A3 --> End([End])
    A1 -.->|write| DB[(State Store)]
    A2 -.->|write| DB
    A3 -.->|write| DB
    DB -.->|read| A1
    DB -.->|read| A2
    DB -.->|read| A3

    style Start fill:#41BD9B,stroke:#2d8a70,color:#0a0a0a
    style End fill:#41BD9B,stroke:#2d8a70,color:#0a0a0a
    style A1 fill:#1f3a32,stroke:#41BD9B,color:#e6e6e6
    style A2 fill:#1f3a32,stroke:#41BD9B,color:#e6e6e6
    style A3 fill:#1f3a32,stroke:#41BD9B,color:#e6e6e6
    style DB fill:#F0C75E,stroke:#b8943f,color:#0a0a0a
```

---
layout: default
---

# Why Durable Execution Matters for AI Agents

- **Resume from Last Checkpoint** - Not from the start of the workflow
- **Save on LLM Costs** - No re-execution of expensive calls
- **Predictable Behavior** - Deterministic execution
- **Built-in Observability** - Full execution history
- **Built-in Resiliency** - Automatic retry logic