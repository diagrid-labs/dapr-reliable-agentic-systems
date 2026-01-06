# Build reliable agentic systems with Dapr Workflow

This repo contains demos showcasing the patterns described in [Building Effective Agents](https://www.anthropic.com/engineering/building-effective-agents) by Anthropic.

The demos use Dapr Workflow to implement reliable multi-step processes and the Dapr Conversation API to interact with LLMs. In this case, Ollama is used as the local LLM provider.

## Prerequisites

1. Install a container orchestration tool such as Docker Desktop or Podman.
2. Install [.NET 9 SDK](https://dotnet.microsoft.com/download)
3. Install [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/)
4. Install [Ollama](https://ollama.com/)
5. Initialize Dapr: `dapr init`
6. Run Diagrid Dashboard: `docker run -p 8080:8080 ghcr.io/diagridio/diagrid-dashboard:latest`
