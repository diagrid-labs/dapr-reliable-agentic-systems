---
layout: section
customTheme: .demo/slides/theme/theme.css
---

# Agentic Systems

---
layout: default
customTheme: .demo/slides/theme/theme.css
---

# What are Agentic Systems?

Agentic AI systems use LLMs to make decisions and take actions.

## Key Components

- **LLMs** - Language understanding and generation
- **Tools** - Ability to interact with external systems
- **Memory** - State and context management
- **Planning** - Multi-step reasoning and strategy

---
layout: default
customTheme: .demo/slides/theme/theme.css
---

# Everyone is Building Agents

The AI agent landscape is exploding with new tools and frameworks

## Common Use Cases

- **Customer Service Automation** - Intelligent support agents
- **Data Analysis Agents** - Automated insights and reporting
- **Code Generation Assistants** - AI pair programming
- **Content Creation Systems** - Automated writing and editing

---
layout: default
customTheme: .demo/slides/theme/theme.css
---

# Production Challenges

- **LLM API Failures** - Rate limits, timeouts, service outages
- **Non-deterministic Responses** - Unpredictable outputs
- **State Management** - Tracking context across multiple calls
- **Cost Management** - Failed retries accumulate charges
- **Monitoring and Debugging** - Understanding what went wrong
- **Partial Completion Recovery** - Resuming from failures

## The Cost of Failure

Re-running entire workflows wastes money and time

---
layout: default
customTheme: .demo/slides/theme/theme.css
---

# Agentic Systems ARE Distributed Systems

They involve communication across LLM providers, services, and data stores

## Familiar Challenges

- **Network Reliability** - Calls can fail
- **Service Availability** - Dependencies can be down
- **Data Consistency** - State synchronization
- **Fault Tolerance** - Recovering from failures
- **Observability** - Understanding system behavior

## Good News

We've been here before with microservices - let's apply what we know!
