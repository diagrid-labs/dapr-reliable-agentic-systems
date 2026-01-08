# Space Debris Cleanup - Autonomous Agent Demo

This demo implements a fully autonomous agent using Dapr Workflow that controls a space debris cleanup mission. The agent makes its own decisions about scanning, analyzing, capturing debris, and requesting human approval for high-risk maneuvers.

## Key Features

- **Autonomous Decision-Making**: Agent uses LLM to reason and choose actions
- **Tool Usage**: Scan, analyze, navigate, capture debris, check fuel
- **Human-in-the-Loop**: Requests approval for risky maneuvers with timeout
- **Error Recovery**: Adapts to tool failures and unexpected situations
- **State Persistence**: Continues mission across failures using ContinueAsNew
- **External Events**: Workflow waits for human approval via RaiseEventAsync

## Running the Demo

1. Start the application:
   ```bash
   cd SpaceDebrisAgent
   dapr run -f .
   ```

2. Start a mission using the REST client in `local.http`

3. Monitor the agent's autonomous decision-making in the logs

4. When the agent requests approval, send approval/disapproval via the `/approval` endpoint

## Architecture

- **SpaceDebrisCleanupWorkflow**: Main agent loop with ContinueAsNew pattern
- **AgentReasoningActivity**: LLM-based decision making
- **Tool Activities**: Scan, Capture, Move, Analyze, CheckFuel, RequestApproval
- **External Events**: Human approval with 1-minute timeout

## Endpoints

- `POST /mission/start` - Start a new autonomous cleanup mission
- `GET /mission/status/{instanceId}` - Get mission status and results
- `GET /mission/{instanceId}/decisions` - View agent's decision history
- `POST /mission/{instanceId}/approval` - Send human approval to workflow
