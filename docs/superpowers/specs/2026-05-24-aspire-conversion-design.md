# .NET Aspire conversion — design spec

**Date:** 2026-05-24
**Scope:** Convert six existing .NET 9 Dapr Workflow projects into six independent .NET 10 + .NET Aspire solutions.

## Goal

Each of the following projects becomes its own self-contained .NET Aspire solution that orchestrates a Valkey state store, a Dapr sidecar, the workflow app, and the Diagrid Dev Dashboard:

- `AlienTranslator`
- `AnomalyAnalysis`
- `GalacticAnomalyClassifier`
- `SpaceColonyPlanner`
- `SpaceDebrisAgent`
- `StarshipDiagnostics`

Each solution can be launched with `aspire run` from its own folder. The Diagrid Dev Dashboard is exposed on host port **18080** in every solution. Because only one solution runs at a time, all solutions share the same host ports (Valkey 16379, dashboard 18080) without conflict.

The reference implementation is `/Users/marcduiker/dev/diagrid-labs/dapr-workflow-versioning/EnterpriseDiagnostics`.

## Non-goals

- Combining the six projects into a single mega-solution.
- Changing workflow business logic (Activities, Models, Workflows).
- Replacing the Ollama conversation component.
- Touching the `ConversationTests` project — left as-is, unreferenced by any new solution.
- Renaming or otherwise modifying `index.js`, `Plans/`, `slide-review.md`, root `README.md`, `LICENSE`.

## Per-solution structure

For each project (using `AlienTranslator` as the example), the folder becomes:

```
AlienTranslator/
├── AlienTranslator.sln
├── AlienTranslator.AppHost/
│   ├── AlienTranslator.AppHost.csproj
│   ├── AppHost.cs
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   ├── Properties/launchSettings.json
│   └── Resources/
│       ├── statestore.yaml
│       ├── statestore-dashboard.yaml
│       └── conversation.yaml
├── AlienTranslator.ApiService/
│   ├── AlienTranslator.ApiService.csproj
│   ├── Program.cs
│   ├── AlienTranslator.ApiService.http
│   ├── appsettings.json
│   ├── Properties/launchSettings.json
│   ├── Activities/        (moved verbatim from existing project root)
│   ├── Models/            (moved verbatim)
│   └── Workflows/         (moved verbatim)
└── AlienTranslator.ServiceDefaults/
    ├── AlienTranslator.ServiceDefaults.csproj
    └── Extensions.cs
```

The same pattern applies to the other five projects, substituting their name. The existing project folder doubles as the new solution root — no top-level reshuffling.

### Files moved verbatim into `*.ApiService/`

- `Activities/`, `Models/`, `Workflows/` — every `.cs` file unchanged.
- `appsettings.json` — unchanged.

### Files renamed

- `local.http` → `<Name>.ApiService.http`. Host URLs inside the file are updated to the port Aspire assigns the ApiService (captured from each new `Properties/launchSettings.json` after scaffolding).

### Files deleted

- `<Name>.csproj` at the existing project root (replaced by three new csprojs under sub-folders).
- `dapr.yaml` — Dapr sidecar is now wired up by `.WithDaprSidecar(...)` in `AppHost.cs`.
- `bin/`, `obj/`, `Properties/launchSettings.json` at the existing project root.

## AppHost.cs template

Identical across the six solutions except for the four tokens called out in the table below.

```csharp
using System.Reflection;
using CommunityToolkit.Aspire.Hosting.Dapr;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddDapr();

string executingPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
    ?? throw new("Where am I?");

var cachePassword = builder.AddParameter("cache-password", "state-store-123", secret: true);

var cache = builder
    .AddValkey("cache", 16379, cachePassword)
    .WithContainerName("alien-translator-state")
    .WithDataVolume("alien-translator-state-data");

var workflowApp = builder
    .AddProject<Projects.AlienTranslator_ApiService>("wf-app")
    .WithDaprSidecar(new DaprSidecarOptions
    {
        AppId = "alien-translator-app",
        LogLevel = "debug",
        ResourcesPaths = [ Path.Join(executingPath, "Resources") ],
    });

workflowApp.WaitFor(cache);

builder
    .AddContainer("diagrid-dashboard", "ghcr.io/diagridio/diagrid-dashboard:latest")
    .WithContainerName("diagrid-dashboard")
    .WithBindMount(Path.Join(executingPath, "Resources"), "/app/components")
    .WithEnvironment("COMPONENT_FILE", "/app/components/statestore-dashboard.yaml")
    .WithEnvironment("APP_ID", "diagrid-dashboard")
    .WithHttpEndpoint(port: 18080, targetPort: 8080)
    .WithReference(cache);

builder.Build().Run();
```

### Per-solution variations

| Project | `Projects.*` type | `AppId` (preserved from existing `dapr.yaml`) | Container name | Volume name |
|---|---|---|---|---|
| AlienTranslator | `AlienTranslator_ApiService` | `alien-translator-app` | `alien-translator-state` | `alien-translator-state-data` |
| AnomalyAnalysis | `AnomalyAnalysis_ApiService` | `anomaly-detection-app` | `anomaly-analysis-state` | `anomaly-analysis-state-data` |
| GalacticAnomalyClassifier | `GalacticAnomalyClassifier_ApiService` | `anomaly-routing-app` | `galactic-anomaly-classifier-state` | `galactic-anomaly-classifier-state-data` |
| SpaceColonyPlanner | `SpaceColonyPlanner_ApiService` | `space-colony-planner-app` | `space-colony-planner-state` | `space-colony-planner-state-data` |
| SpaceDebrisAgent | `SpaceDebrisAgent_ApiService` | `space-debris-agent` | `space-debris-agent-state` | `space-debris-agent-state-data` |
| StarshipDiagnostics | `StarshipDiagnostics_ApiService` | `starship-diagnostics-app` | `starship-diagnostics-state` | `starship-diagnostics-state-data` |

### Fixed across all six solutions

| Setting | Value |
|---|---|
| Valkey host port | 16379 |
| Valkey password | `state-store-123` |
| Diagrid Dashboard host port | 18080 |
| Dapr state store component name | `statestore` |
| Ollama conversation component name | `conversation` |

## Resources YAML files (per-solution `AppHost/Resources/`)

Three files per solution, identical content across all six solutions.

### `statestore.yaml`

```yaml
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: statestore
spec:
  type: state.redis
  version: v1
  metadata:
    - name: redisHost
      value: "localhost:16379"
    - name: redisPassword
      value: "state-store-123"
    - name: actorStateStore
      value: "true"
```

### `statestore-dashboard.yaml`

Scoped to the `diagrid-dashboard` Dapr sidecar; uses `host.docker.internal` because the dashboard runs in a container while Valkey is published on the host.

```yaml
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: statestore
scopes:
  - diagrid-dashboard
spec:
  type: state.redis
  version: v1
  metadata:
    - name: redisHost
      value: "host.docker.internal:16379"
    - name: redisPassword
      value: "state-store-123"
    - name: actorStateStore
      value: "true"
```

### `conversation.yaml`

Unchanged from the current repo-root `Resources/conversation.yaml`. Ollama runs on the host (default `http://localhost:11434`); it is not orchestrated by Aspire and remains an external prerequisite.

```yaml
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: conversation
spec:
  type: conversation.ollama
  version: v1
  metadata:
    - name: model
      value: llama3.2:3b
```

## `*.ApiService.csproj`

Per-solution example (`AlienTranslator.ApiService.csproj`):

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>AlienTranslator</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\AlienTranslator.ServiceDefaults\AlienTranslator.ServiceDefaults.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Dapr.AspNetCore"         Version="1.17.9" />
    <PackageReference Include="Dapr.Client"             Version="1.17.9" />
    <PackageReference Include="Dapr.Workflow"           Version="1.17.9" />
    <PackageReference Include="Dapr.Workflow.Analyzers" Version="1.17.9" />
    <PackageReference Include="Dapr.AI"                 Version="1.17.9" />
  </ItemGroup>

</Project>
```

`RootNamespace` is set to the existing project name so existing `namespace <Name>.Activities` / `.Models` / `.Workflows` declarations keep compiling unchanged.

**Risk:** if `1.17.9` is not available on NuGet for one of the Dapr packages at implementation time, the closest published `1.17.x` for that package will be used and noted in the implementation plan.

## `Program.cs` changes

Two added lines per project, no removals. Example for AlienTranslator:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();                          // ADDED

builder.Services.ConfigureHttpJsonOptions(options => { /* existing */ });
builder.Services.AddDaprConversationClient();
builder.Services.AddDaprWorkflow(options => { /* existing — unchanged */ });

var app = builder.Build();

// existing endpoints unchanged

app.MapDefaultEndpoints();                             // ADDED

app.Run();
```

`AddServiceDefaults()` resolves from the `Microsoft.Extensions.Hosting` namespace (already in scope via implicit usings). `MapDefaultEndpoints()` registers `/health` and `/alive` in Development only.

## `*.ServiceDefaults` project

Verbatim copy of `EnterpriseDiagnostics.ServiceDefaults` from the reference solution, renamed per solution.

### `*.ServiceDefaults.csproj` (example)

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsAspireSharedProject>true</IsAspireSharedProject>
  </PropertyGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
    <PackageReference Include="Microsoft.Extensions.Http.Resilience"        Version="9.*" />
    <PackageReference Include="Microsoft.Extensions.ServiceDiscovery"       Version="13.2.4" />
    <PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.*" />
    <PackageReference Include="OpenTelemetry.Extensions.Hosting"            Version="1.*" />
    <PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore"    Version="1.*" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Http"          Version="1.*" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Runtime"       Version="1.*" />
  </ItemGroup>

</Project>
```

### `Extensions.cs`

Identical across all six solutions. Namespace is `Microsoft.Extensions.Hosting` so `builder.AddServiceDefaults()` resolves without an additional `using` directive. Provides:

- `AddServiceDefaults()` — OpenTelemetry (logs, metrics, traces), default health checks, service discovery, HTTP client resilience.
- `MapDefaultEndpoints()` — `/health` and `/alive` endpoints in Development only.

Source is copied verbatim from `/Users/marcduiker/dev/diagrid-labs/dapr-workflow-versioning/EnterpriseDiagnostics/EnterpriseDiagnostics.ServiceDefaults/Extensions.cs`.

## `*.AppHost.csproj`

Per-solution example:

```xml
<Project Sdk="Aspire.AppHost.Sdk/13.3.5">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <UserSecretsId>{generated-guid-per-solution}</UserSecretsId>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\AlienTranslator.ApiService\AlienTranslator.ApiService.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Aspire.Hosting.Valkey"               Version="13.3.5" />
    <PackageReference Include="CommunityToolkit.Aspire.Hosting.Dapr" Version="13.0.0" />
  </ItemGroup>

  <ItemGroup>
    <Content Include="Resources\**\*.*">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
      <Link>Resources\%(RecursiveDir)%(Filename)%(Extension)</Link>
    </Content>
  </ItemGroup>

</Project>
```

A fresh `UserSecretsId` GUID is generated per AppHost so the `cache-password` parameter isolates per solution.

### Pinned package versions

| Package | Version |
|---|---|
| `Aspire.AppHost.Sdk` | 13.3.5 |
| `Aspire.Hosting.Valkey` | 13.3.5 |
| `CommunityToolkit.Aspire.Hosting.Dapr` | 13.0.0 |
| `Microsoft.Extensions.ServiceDiscovery` (in ServiceDefaults) | 13.2.4 |

## Repo-level changes

### Deletions

- `dapr-reliable-agentic-systems.sln` at repo root — replaced by six per-project `.sln` files.
- `Resources/` at repo root (containing `statestore.yaml` and `conversation.yaml`) — replaced by per-AppHost `Resources/` folders.

### Preservations

- `ConversationTests/` — untouched. No longer referenced by any solution; remains as a standalone folder.
- `index.js`, `Plans/`, `slide-review.md`, root `README.md`, `LICENSE` — untouched.

### `AGENTS.md` update

Replace the `# Back-end development rules` bullets that pin .NET 9 / Dapr 1.16.1 with the new conventions:

- Each project is a .NET Aspire solution composed of three csprojs: `<Name>.AppHost` (`Aspire.AppHost.Sdk` 13.3.5, net10.0), `<Name>.ApiService` (`Microsoft.NET.Sdk.Web`, net10.0), `<Name>.ServiceDefaults` (`Microsoft.NET.Sdk`, net10.0).
- The ApiService targets net10.0 and uses Dapr packages at version 1.17.9 (`Dapr.AspNetCore`, `Dapr.Client`, `Dapr.Workflow`, `Dapr.Workflow.Analyzers`, `Dapr.AI`).
- The AppHost orchestrates Valkey (port 16379, password-protected), the ApiService with a Dapr sidecar (component name `statestore`), and a `diagrid-dashboard` container on port 18080. Dapr components live in `<Name>.AppHost/Resources/`.
- Run a solution with `aspire run` from the solution root. Do not use `dapr run` or `dapr.yaml` — Aspire owns the sidecar lifecycle.
- `Program.cs` calls `builder.AddServiceDefaults()` before service registration and `app.MapDefaultEndpoints()` before `app.Run()`.

The HTTP file rule (one entry per endpoint) stays; only the filename changes to `<Name>.ApiService.http`.

### Per-project `README.md` updates

Each of the six existing `README.md` files gets its "Running the application" section rewritten, modeled on `EnterpriseDiagnostics/README.md`:

- Replace `dapr run -f dapr.yaml` instructions with `aspire run`.
- List new prerequisites: .NET 10 SDK, Aspire CLI, Docker or Podman, Dapr CLI 1.17+, Ollama with `llama3.2:3b` pulled.
- Point at the Aspire dashboard for the `diagrid-dashboard` endpoint link (port 18080).

Existing content describing the workflow logic (architecture, mermaid diagrams, API examples, sample payloads) is preserved verbatim per project.

## Verification per solution

After scaffolding each solution, the following must pass before that solution is considered complete:

1. `dotnet build <Name>.sln` succeeds with zero errors.
2. `aspire run` from the solution root starts all four resources (Valkey, ApiService, Dapr sidecar, diagrid-dashboard) and the Aspire dashboard opens automatically.
3. The diagrid-dashboard endpoint is reachable at `http://localhost:18080` and lists the workflow's state store data.
4. The existing workflow's primary HTTP endpoint (as captured in the renamed `<Name>.ApiService.http` file) returns a successful response and the resulting workflow instance status can be retrieved.

## Open risks

- **Dapr 1.17.9 availability:** if any of the five Dapr packages does not have 1.17.9 on NuGet at implementation time, the closest published 1.17.x for that package is used and called out in the implementation plan.
- **HTTP file ports:** the ApiService port is assigned by Aspire's launch profile per solution; the `<Name>.ApiService.http` host URL must be updated after the AppHost is scaffolded and the actual port is known.
- **Ollama prerequisite:** users must have Ollama running locally with `llama3.2:3b` pulled before `aspire run` — same prerequisite as the current setup.
