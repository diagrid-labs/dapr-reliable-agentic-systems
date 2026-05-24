# Alien Translator - Evaluator-Optimizer Pattern

This demo implements an **evaluator-optimizer workflow** using Dapr Workflow for alien language translation refinement. The system generates an initial translation, then iteratively evaluates and refines it through multiple rounds until it meets quality standards.

## Pattern Overview

The evaluator-optimizer pattern separates translation generation from quality evaluation:

1. **Translator** (TranslateActivity/RefineTranslationActivity) - Generates and improves translations
2. **Evaluator** (EvaluateTranslationActivity) - Assesses quality and provides feedback
3. **Workflow** (EvaluatorOptimizerWorkflow) - Orchestrates the iterative refinement loop

### Benefits

- ✅ Iterative quality improvement with measurable metrics
- ✅ Specialized LLM roles (translator vs. evaluator)
- ✅ Detailed feedback loop for targeted refinements
- ✅ Graceful degradation with max iteration limit

### Drawbacks

- ❌ Higher latency (multiple LLM calls per translation)
- ❌ More expensive (10+ LLM calls for 5 iterations)
- ❌ Potential diminishing returns in later iterations
- ❌ Evaluation subjectivity (LLM can't verify true accuracy)

### When to Use

Use this pattern when quality is more important than speed and iterative refinement demonstrably improves results. Ideal for diplomatic communications, literary translation, and content requiring editorial review.

## Architecture

```mermaid
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
    
    style Start fill:#e1f5ff
    style Success fill:#d4edda
    style MaxReached fill:#fff3cd
    style Gate1 fill:#fff3cd
    style Gate2 fill:#fff3cd
    style Restart fill:#e1f5ff
```

### Workflow Logic

1. Generate initial translation
2. Loop (max 5 iterations):
   - Evaluate current translation
   - If quality ≥ 8.0 and meets standards → Success!
   - If max iterations reached → Return best effort
   - Refine translation based on feedback
3. Return final translation with iteration history

### Quality Metrics

- **Accuracy Score** (0-10): Faithfulness to original meaning
- **Cultural Nuance Score** (0-10): Preservation of cultural context
- **Idiomatic Score** (0-10): Natural English readability
- **Overall Quality** (0-10): Holistic assessment

## Running the Demo

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download)
- [Aspire CLI](https://aspire.dev/get-started/install-cli/) — install with `dotnet tool install -g Aspire.Cli`
- [Docker](https://www.docker.com/products/docker-desktop/) or [Podman](https://podman.io/docs/installation)
- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/) (version 1.17+)
- [Ollama](https://ollama.com/) with the `llama3.2:3b` model pulled

### Start Ollama

```bash
ollama serve
ollama pull llama3.2:3b
```

### Run the Application

From the `AlienTranslator/` folder:

```bash
aspire run
```

This launches the Aspire AppHost, which orchestrates:
- A Valkey container for workflow state persistence (port 16379, password-protected)
- The ApiService with a Dapr sidecar (app ID `alien-translator-app`)
- The Diagrid Dev Dashboard container on http://localhost:18080

The Aspire dashboard opens automatically in the browser, showing all resources and their status.

### Test with REST Client

Open `AlienTranslator.ApiService/AlienTranslator.ApiService.http` in VS Code and execute the requests to translate alien texts. The ApiService HTTP port is shown in the Aspire dashboard.

### Inspect the Workflow runs

Open the Diagrid Dev Dashboard at `http://localhost:18080` and inspect the workflow executions.
