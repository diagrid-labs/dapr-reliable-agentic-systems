# .NET Aspire Conversion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Convert the six existing .NET 9 Dapr Workflow projects (AlienTranslator, AnomalyAnalysis, GalacticAnomalyClassifier, SpaceColonyPlanner, SpaceDebrisAgent, StarshipDiagnostics) into six independent .NET 10 + .NET Aspire solutions, each orchestrating Valkey + Dapr sidecar + the workflow ApiService + a Diagrid Dev Dashboard on host port 18080.

**Architecture:** Per project, scaffold three csprojs (`*.AppHost`, `*.ApiService`, `*.ServiceDefaults`) following the reference layout at `/Users/marcduiker/dev/diagrid-labs/dapr-workflow-versioning/EnterpriseDiagnostics`. The existing project folder becomes the solution root; existing source files (`Activities/`, `Models/`, `Workflows/`, `Program.cs`, `appsettings.json`) move into `*.ApiService/`. Dapr components live in `*.AppHost/Resources/`. The repo-root `.sln` and `Resources/` folder are deleted; `AGENTS.md` is updated to reflect the new conventions. `ConversationTests/` is untouched.

**Tech Stack:** .NET 10, .NET Aspire 13.3.5 (AppHost SDK + Aspire.Hosting.Valkey), CommunityToolkit.Aspire.Hosting.Dapr 13.0.0, Dapr 1.17.9 (Dapr.AspNetCore, Dapr.Client, Dapr.Workflow, Dapr.Workflow.Analyzers, Dapr.AI), Valkey (state store), Ollama (conversation, external), Diagrid Dev Dashboard container.

**Spec reference:** `docs/superpowers/specs/2026-05-24-aspire-conversion-design.md`

---

## File Structure

For each of the six projects (using `<P>` as placeholder for the project name, e.g. `AlienTranslator`), the existing `<P>/` folder is restructured to:

```
<P>/
├── <P>.sln                                (NEW)
├── README.md                              (UPDATED — Running section rewritten)
├── <P>.AppHost/                           (NEW directory)
│   ├── <P>.AppHost.csproj
│   ├── AppHost.cs
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   ├── Properties/launchSettings.json
│   └── Resources/
│       ├── statestore.yaml
│       ├── statestore-dashboard.yaml
│       └── conversation.yaml
├── <P>.ApiService/                        (NEW directory; receives existing source)
│   ├── <P>.ApiService.csproj
│   ├── Program.cs                         (existing, + 2 lines)
│   ├── <P>.ApiService.http                (renamed from existing local.http)
│   ├── appsettings.json                   (moved from existing project root)
│   ├── Properties/launchSettings.json
│   ├── Activities/                        (moved verbatim)
│   ├── Models/                            (moved verbatim)
│   └── Workflows/                         (moved verbatim)
└── <P>.ServiceDefaults/                   (NEW directory)
    ├── <P>.ServiceDefaults.csproj
    └── Extensions.cs
```

**Files deleted from each existing `<P>/` folder during conversion:**
- `<P>/<P>.csproj` (old single csproj)
- `<P>/dapr.yaml`
- `<P>/local.http` (renamed and moved into ApiService/)
- `<P>/Program.cs` (moved into ApiService/)
- `<P>/appsettings.json` (moved into ApiService/)
- `<P>/Activities/`, `<P>/Models/`, `<P>/Workflows/` (moved into ApiService/)
- `<P>/Properties/launchSettings.json` and `<P>/Properties/` if empty
- `<P>/bin/`, `<P>/obj/`

**Repo-root files deleted at the end:**
- `dapr-reliable-agentic-systems.sln`
- `Resources/` (the folder containing `statestore.yaml` and `conversation.yaml`)

---

## Shared Templates

These files are byte-identical across all six solutions. Per-project tasks reference them by name (e.g. "Template A"). To avoid placeholder ambiguity, the entire content of every template is inlined below.

### Template A — `*.ServiceDefaults/Extensions.cs`

Identical to the reference `EnterpriseDiagnostics.ServiceDefaults/Extensions.cs`. Namespace `Microsoft.Extensions.Hosting` so `builder.AddServiceDefaults()` resolves without an extra `using`.

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ServiceDiscovery;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

// Adds common Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
// This project should be referenced by each service project in your solution.
// To learn more about using this project, see https://aka.ms/dotnet/aspire/service-defaults
public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Turn on resilience by default
            http.AddStandardResilienceHandler();

            // Turn on service discovery by default
            http.AddServiceDiscovery();
        });

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(builder.Environment.ApplicationName)
                    .AddAspNetCoreInstrumentation(tracing =>
                        tracing.Filter = context =>
                            !context.Request.Path.StartsWithSegments(HealthEndpointPath)
                            && !context.Request.Path.StartsWithSegments(AlivenessEndpointPath)
                    )
                    .AddHttpClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapHealthChecks(HealthEndpointPath);

            app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains("live")
            });
        }

        return app;
    }
}
```

### Template B — `*.ServiceDefaults/<P>.ServiceDefaults.csproj`

Only the filename varies — content is identical across all six.

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
    <PackageReference Include="Microsoft.Extensions.Http.Resilience" Version="10.1.0" />
    <PackageReference Include="Microsoft.Extensions.ServiceDiscovery" Version="10.1.0" />
    <PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.15.3" />
    <PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.15.3" />
    <PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.15.2" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="1.15.1" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Runtime" Version="1.15.1" />
  </ItemGroup>

</Project>
```

### Template C — `*.AppHost/Resources/statestore.yaml`

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

### Template D — `*.AppHost/Resources/statestore-dashboard.yaml`

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

### Template E — `*.AppHost/Resources/conversation.yaml`

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

### Template F — `*.AppHost/appsettings.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Aspire.Hosting.Dcp": "Warning"
    }
  }
}
```

### Template G — `*.AppHost/appsettings.Development.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

### Template H — `*.AppHost/Properties/launchSettings.json`

Ports are unique per project (so multiple solutions don't collide if a user accidentally launches two). The five `ASPIRE_*` ports are reserved per project from the ranges below; the `applicationUrl` is the Aspire dashboard URL.

| Project | dashboard | OTLP | MCP | resource | (reserved gap) |
|---|---|---|---|---|---|
| AlienTranslator | 15001 | 19001 | 18101 | 20001 | — |
| AnomalyAnalysis | 15002 | 19002 | 18102 | 20002 | — |
| GalacticAnomalyClassifier | 15003 | 19003 | 18103 | 20003 | — |
| SpaceColonyPlanner | 15004 | 19004 | 18104 | 20004 | — |
| SpaceDebrisAgent | 15005 | 19005 | 18105 | 20005 | — |
| StarshipDiagnostics | 15006 | 19006 | 18106 | 20006 | — |

```json
{
  "$schema": "https://json.schemastore.org/launchsettings.json",
  "profiles": {
    "http": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "applicationUrl": "https://localhost:<DASHBOARD_PORT>",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development",
        "DOTNET_ENVIRONMENT": "Development",
        "ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL": "https://localhost:<OTLP_PORT>",
        "ASPIRE_DASHBOARD_MCP_ENDPOINT_URL": "https://localhost:<MCP_PORT>",
        "ASPIRE_RESOURCE_SERVICE_ENDPOINT_URL": "https://localhost:<RESOURCE_PORT>"
      }
    }
  }
}
```

Substitute `<DASHBOARD_PORT>`, `<OTLP_PORT>`, `<MCP_PORT>`, `<RESOURCE_PORT>` from the table when writing the file.

### Template I — `*.ApiService/Properties/launchSettings.json`

```json
{
  "$schema": "https://json.schemastore.org/launchsettings.json",
  "profiles": {
    "http": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": false,
      "applicationUrl": "http://localhost:5500",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

> The actual ApiService port is reassigned by Aspire at runtime; this file just satisfies `dotnet run` outside Aspire.

### Template J — `*.ApiService/appsettings.Development.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

---

## Task 0: Preflight check

**Files:** none (verification only)

- [ ] **Step 1: Verify required tooling is installed**

Run:
```bash
dotnet --version          # expect 10.0.x
aspire --version          # expect installed
docker info > /dev/null && echo "docker ok"   # or podman info
dapr --version            # expect CLI 1.17+
curl -s http://localhost:11434/api/tags > /dev/null && echo "ollama ok"
```

Expected: all five commands succeed. If `dotnet --version` returns < 10.0, install the .NET 10 SDK from https://dotnet.microsoft.com/. If `aspire` is missing, run `dotnet tool install -g Aspire.Cli`. If Ollama is missing or the `llama3.2:3b` model is not pulled (`ollama list | grep llama3.2:3b`), run `ollama pull llama3.2:3b`.

- [ ] **Step 2: Confirm reference solution builds (sanity check)**

Run:
```bash
dotnet build /Users/marcduiker/dev/diagrid-labs/dapr-workflow-versioning/EnterpriseDiagnostics/EnterpriseDiagnostics.sln
```

Expected: succeeds with 0 errors. If this fails, the local environment lacks something the new solutions will also need. Resolve before continuing.

- [ ] **Step 3: Confirm Dapr 1.17.9 packages exist on NuGet**

Run (works in both bash and zsh — uses `tr` instead of bash-specific lowercase expansion):
```sh
for pkg in dapr.aspnetcore dapr.client dapr.workflow dapr.workflow.analyzers dapr.ai; do
  printf "%s 1.17.9: " "$pkg"
  curl -s -o /dev/null -w "%{http_code}\n" "https://api.nuget.org/v3-flatcontainer/${pkg}/1.17.9/${pkg}.1.17.9.nupkg"
done
```

Expected: each line ends with `200`. If any returns `404`, run `curl -s "https://api.nuget.org/v3-flatcontainer/<pkg>/index.json" | grep -o '1\.17\.[0-9]*' | sort -V | tail -1` to find the closest 1.17.x, and record the pinned versions in the commit message of Task 1 Step 20 below.

---

## Task 1: Convert AlienTranslator (reference conversion)

This is the most-detailed task; Tasks 2–6 follow the same pattern with only the per-project tokens changed.

**Per-project tokens:**

| Token | Value |
|---|---|
| `<P>` (folder + namespace prefix) | `AlienTranslator` |
| `<AppId>` | `alien-translator-app` |
| `<ContainerName>` | `alien-translator-state` |
| `<VolumeName>` | `alien-translator-state-data` |
| Dashboard port (launchSettings) | 15001 |
| OTLP port | 19001 |
| MCP port | 18101 |
| Resource port | 20001 |

**Files (new):**
- Create: `AlienTranslator/AlienTranslator.sln`
- Create: `AlienTranslator/AlienTranslator.AppHost/AlienTranslator.AppHost.csproj`
- Create: `AlienTranslator/AlienTranslator.AppHost/AppHost.cs`
- Create: `AlienTranslator/AlienTranslator.AppHost/appsettings.json`
- Create: `AlienTranslator/AlienTranslator.AppHost/appsettings.Development.json`
- Create: `AlienTranslator/AlienTranslator.AppHost/Properties/launchSettings.json`
- Create: `AlienTranslator/AlienTranslator.AppHost/Resources/statestore.yaml`
- Create: `AlienTranslator/AlienTranslator.AppHost/Resources/statestore-dashboard.yaml`
- Create: `AlienTranslator/AlienTranslator.AppHost/Resources/conversation.yaml`
- Create: `AlienTranslator/AlienTranslator.ApiService/AlienTranslator.ApiService.csproj`
- Create: `AlienTranslator/AlienTranslator.ApiService/Properties/launchSettings.json`
- Create: `AlienTranslator/AlienTranslator.ApiService/appsettings.Development.json`
- Create: `AlienTranslator/AlienTranslator.ServiceDefaults/AlienTranslator.ServiceDefaults.csproj`
- Create: `AlienTranslator/AlienTranslator.ServiceDefaults/Extensions.cs`

**Files (moved):**
- Move: `AlienTranslator/Program.cs` → `AlienTranslator/AlienTranslator.ApiService/Program.cs`
- Move: `AlienTranslator/appsettings.json` → `AlienTranslator/AlienTranslator.ApiService/appsettings.json`
- Move: `AlienTranslator/Activities/` → `AlienTranslator/AlienTranslator.ApiService/Activities/`
- Move: `AlienTranslator/Models/` → `AlienTranslator/AlienTranslator.ApiService/Models/`
- Move: `AlienTranslator/Workflows/` → `AlienTranslator/AlienTranslator.ApiService/Workflows/`
- Move + rename: `AlienTranslator/local.http` → `AlienTranslator/AlienTranslator.ApiService/AlienTranslator.ApiService.http`

**Files (deleted):**
- Delete: `AlienTranslator/AlienTranslator.csproj`
- Delete: `AlienTranslator/dapr.yaml`
- Delete: `AlienTranslator/Properties/` (entire folder, after launchSettings.json moves)
- Delete: `AlienTranslator/bin/`, `AlienTranslator/obj/`

---

- [ ] **Step 1: Create the three new directories and move existing source into `*.ApiService/`**

Run:
```bash
cd /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems/AlienTranslator
mkdir -p AlienTranslator.AppHost/Resources AlienTranslator.AppHost/Properties
mkdir -p AlienTranslator.ApiService/Properties
mkdir -p AlienTranslator.ServiceDefaults
git mv Program.cs           AlienTranslator.ApiService/Program.cs
git mv appsettings.json     AlienTranslator.ApiService/appsettings.json
git mv Activities           AlienTranslator.ApiService/Activities
git mv Models               AlienTranslator.ApiService/Models
git mv Workflows            AlienTranslator.ApiService/Workflows
git mv local.http           AlienTranslator.ApiService/AlienTranslator.ApiService.http
```

Expected: `git status` shows the renames as moves (R). If `local.http` does not exist, skip the last `git mv` and create `AlienTranslator.ApiService/AlienTranslator.ApiService.http` from scratch in Step 9.

- [ ] **Step 2: Write `AlienTranslator/AlienTranslator.ServiceDefaults/AlienTranslator.ServiceDefaults.csproj`** (Template B)

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
    <PackageReference Include="Microsoft.Extensions.Http.Resilience" Version="10.1.0" />
    <PackageReference Include="Microsoft.Extensions.ServiceDiscovery" Version="10.1.0" />
    <PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.15.3" />
    <PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.15.3" />
    <PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.15.2" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="1.15.1" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Runtime" Version="1.15.1" />
  </ItemGroup>

</Project>
```

- [ ] **Step 3: Write `AlienTranslator/AlienTranslator.ServiceDefaults/Extensions.cs`** (Template A)

Copy Template A verbatim from the "Shared Templates" section above into this file. Do not modify the namespace; it must remain `Microsoft.Extensions.Hosting`.

- [ ] **Step 4: Write `AlienTranslator/AlienTranslator.ApiService/AlienTranslator.ApiService.csproj`**

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
    <PackageReference Include="Dapr.AspNetCore" Version="1.17.9" />
    <PackageReference Include="Dapr.Client" Version="1.17.9" />
    <PackageReference Include="Dapr.Workflow" Version="1.17.9" />
    <PackageReference Include="Dapr.Workflow.Analyzers" Version="1.17.9" />
    <PackageReference Include="Dapr.AI" Version="1.17.9" />
  </ItemGroup>

</Project>
```

> If Task 0 Step 3 found any Dapr package not published at 1.17.9, replace the Version for that package with the closest 1.17.x discovered.

- [ ] **Step 5: Write `AlienTranslator/AlienTranslator.ApiService/Properties/launchSettings.json`** (Template I)

```json
{
  "$schema": "https://json.schemastore.org/launchsettings.json",
  "profiles": {
    "http": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": false,
      "applicationUrl": "http://localhost:5500",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

- [ ] **Step 6: Write `AlienTranslator/AlienTranslator.ApiService/appsettings.Development.json`** (Template J)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

- [ ] **Step 7: Edit `AlienTranslator/AlienTranslator.ApiService/Program.cs` — add two lines**

Open the moved file. After the line `var builder = WebApplication.CreateBuilder(args);`, insert a blank line and `builder.AddServiceDefaults();`. Immediately before the final `app.Run();` line, insert `app.MapDefaultEndpoints();` and a blank line.

After edits, the file's top should look like:

```csharp
using Microsoft.AspNetCore.Mvc;
using Dapr.AI.Conversation.Extensions;
using Dapr.Workflow;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.ConfigureHttpJsonOptions(options =>
// ... existing content unchanged ...
```

And the file's bottom should look like:

```csharp
// ... existing endpoints unchanged ...

app.MapDefaultEndpoints();

app.Run();
```

- [ ] **Step 8: Edit `AlienTranslator/AlienTranslator.ApiService/AlienTranslator.ApiService.http` — update host port placeholder**

Replace any line beginning with `@host` or with the literal string `http://localhost:5500` with:
```
@host = http://localhost:5500
```
The actual Aspire-assigned port will be substituted after the first successful `aspire run` in Step 17.

If `local.http` did not exist in the original project, create this file with a placeholder request:
```
@host = http://localhost:5500

###
GET {{host}}/
```

- [ ] **Step 9: Write `AlienTranslator/AlienTranslator.AppHost/AlienTranslator.AppHost.csproj`**

Generate a fresh GUID for `<UserSecretsId>` first:
```bash
uuidgen | tr '[:upper:]' '[:lower:]'
```

Then write the file with that GUID substituted in:

```xml
<Project Sdk="Aspire.AppHost.Sdk/13.3.5">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <UserSecretsId>PASTE-GUID-HERE</UserSecretsId>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\AlienTranslator.ApiService\AlienTranslator.ApiService.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Aspire.Hosting.Valkey" Version="13.3.5" />
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

- [ ] **Step 10: Write `AlienTranslator/AlienTranslator.AppHost/AppHost.cs`**

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

- [ ] **Step 11: Write `AlienTranslator/AlienTranslator.AppHost/appsettings.json`** (Template F)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Aspire.Hosting.Dcp": "Warning"
    }
  }
}
```

- [ ] **Step 12: Write `AlienTranslator/AlienTranslator.AppHost/appsettings.Development.json`** (Template G)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

- [ ] **Step 13: Write `AlienTranslator/AlienTranslator.AppHost/Properties/launchSettings.json`** (Template H with AlienTranslator ports)

```json
{
  "$schema": "https://json.schemastore.org/launchsettings.json",
  "profiles": {
    "http": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "applicationUrl": "https://localhost:15001",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development",
        "DOTNET_ENVIRONMENT": "Development",
        "ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL": "https://localhost:19001",
        "ASPIRE_DASHBOARD_MCP_ENDPOINT_URL": "https://localhost:18101",
        "ASPIRE_RESOURCE_SERVICE_ENDPOINT_URL": "https://localhost:20001"
      }
    }
  }
}
```

- [ ] **Step 14: Write the three Resources YAML files** (Templates C, D, E)

Write each file with the exact content from the corresponding template above:
- `AlienTranslator/AlienTranslator.AppHost/Resources/statestore.yaml` (Template C)
- `AlienTranslator/AlienTranslator.AppHost/Resources/statestore-dashboard.yaml` (Template D)
- `AlienTranslator/AlienTranslator.AppHost/Resources/conversation.yaml` (Template E)

- [ ] **Step 15: Write `AlienTranslator/AlienTranslator.sln`**

Generate three fresh GUIDs (one per csproj) and one solution GUID:
```bash
for i in 1 2 3 4; do uuidgen | tr '[:lower:]' '[:upper:]'; done
```

Then write the file (substitute `GUID-APPHOST`, `GUID-APISERVICE`, `GUID-SERVICEDEFAULTS`, `GUID-SOLUTION` with the four generated UUIDs):

```
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.8.0.0
MinimumVisualStudioVersion = 17.8.0.0
Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "AlienTranslator.AppHost", "AlienTranslator.AppHost\AlienTranslator.AppHost.csproj", "{GUID-APPHOST}"
EndProject
Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "AlienTranslator.ServiceDefaults", "AlienTranslator.ServiceDefaults\AlienTranslator.ServiceDefaults.csproj", "{GUID-SERVICEDEFAULTS}"
EndProject
Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "AlienTranslator.ApiService", "AlienTranslator.ApiService\AlienTranslator.ApiService.csproj", "{GUID-APISERVICE}"
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Release|Any CPU = Release|Any CPU
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{GUID-APPHOST}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{GUID-APPHOST}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{GUID-APPHOST}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{GUID-APPHOST}.Release|Any CPU.Build.0 = Release|Any CPU
		{GUID-SERVICEDEFAULTS}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{GUID-SERVICEDEFAULTS}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{GUID-SERVICEDEFAULTS}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{GUID-SERVICEDEFAULTS}.Release|Any CPU.Build.0 = Release|Any CPU
		{GUID-APISERVICE}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{GUID-APISERVICE}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{GUID-APISERVICE}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{GUID-APISERVICE}.Release|Any CPU.Build.0 = Release|Any CPU
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
	GlobalSection(ExtensibilityGlobals) = postSolution
		SolutionGuid = {GUID-SOLUTION}
	EndGlobalSection
EndGlobal
```

> Alternative: instead of hand-writing the .sln, use `dotnet sln AlienTranslator/AlienTranslator.sln add` (see commands). If using that approach, run `dotnet new sln -n AlienTranslator -o AlienTranslator` first, then `dotnet sln AlienTranslator/AlienTranslator.sln add` for each of the three csprojs. Either approach is acceptable.

- [ ] **Step 16: Delete the obsolete files from the existing project root**

```bash
cd /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems/AlienTranslator
git rm AlienTranslator.csproj
git rm dapr.yaml
git rm -r Properties 2>/dev/null || true
rm -rf bin obj
```

Expected: `git status` shows three `D` entries (csproj, dapr.yaml, Properties/launchSettings.json). If any of those paths did not exist in the original, the `git rm` line for it errors out — that's fine.

- [ ] **Step 17: Build the solution**

```bash
cd /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems
dotnet build AlienTranslator/AlienTranslator.sln
```

Expected: build succeeds with 0 errors. If errors mention `Projects.AlienTranslator_ApiService` not found, do `dotnet restore AlienTranslator/AlienTranslator.AppHost/AlienTranslator.AppHost.csproj` (the `Projects.*` type is generated by Aspire's source generator at restore time) and rebuild.

- [ ] **Step 18: Smoke run with Aspire and capture the ApiService port**

Run (manually — `aspire run` is interactive; run it in a separate terminal):
```bash
cd /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems/AlienTranslator
aspire run
```

Then in another terminal, verify:
```bash
curl -s http://localhost:18080/        # diagrid-dashboard reachable
curl -s http://localhost:16379         # Valkey port open (connection-refused-then-closes is OK; "Connection refused" is NOT)
```

Open `https://localhost:15001` in a browser — the Aspire dashboard should list four resources: `cache`, `wf-app`, `wf-app-dapr`, `diagrid-dashboard`, all running. Click the `wf-app` endpoint link and note the HTTP URL (e.g. `http://localhost:5234`).

Stop `aspire run` with Ctrl+C once verified.

- [ ] **Step 19: Update `AlienTranslator.ApiService.http` with the actual port**

Edit `AlienTranslator/AlienTranslator.ApiService/AlienTranslator.ApiService.http` and replace the `@host` value with the URL captured in Step 18. Example:
```
@host = http://localhost:5234
```

If the existing `local.http` had endpoint blocks (POST/GET), preserve those exactly — only the `@host` value (or the inline URL in each request, if there is no `@host` variable) changes.

- [ ] **Step 20: Commit**

```bash
cd /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems
git add AlienTranslator
git commit -m "feat(AlienTranslator): convert to .NET Aspire solution"
```

> If Task 0 Step 3 required pinning any Dapr package to a version other than 1.17.9, mention the actual versions in the commit body.

---

## Task 2: Convert AnomalyAnalysis

Same shape as Task 1 with project-specific tokens substituted. The following per-project values replace the corresponding ones from Task 1 wherever they appear (`<P>`, `<AppId>`, `<ContainerName>`, `<VolumeName>`, ports, etc.):

| Token | Value |
|---|---|
| `<P>` | `AnomalyAnalysis` |
| `<AppId>` | `anomaly-detection-app` |
| `<ContainerName>` | `anomaly-analysis-state` |
| `<VolumeName>` | `anomaly-analysis-state-data` |
| Dashboard port | 15002 |
| OTLP port | 19002 |
| MCP port | 18102 |
| Resource port | 20002 |
| ApiService `Projects.*` type | `Projects.AnomalyAnalysis_ApiService` |

**Files (new):** Same 14 new files as Task 1, with `AlienTranslator` replaced by `AnomalyAnalysis` in every path.

**Files (moved/deleted):** Same moves/deletes as Task 1, applied to `AnomalyAnalysis/` instead of `AlienTranslator/`.

---

- [ ] **Step 1: Create directories and move source files**

```bash
cd /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems/AnomalyAnalysis
mkdir -p AnomalyAnalysis.AppHost/Resources AnomalyAnalysis.AppHost/Properties
mkdir -p AnomalyAnalysis.ApiService/Properties
mkdir -p AnomalyAnalysis.ServiceDefaults
git mv Program.cs       AnomalyAnalysis.ApiService/Program.cs
git mv appsettings.json AnomalyAnalysis.ApiService/appsettings.json
git mv Activities       AnomalyAnalysis.ApiService/Activities
git mv Models           AnomalyAnalysis.ApiService/Models
git mv Workflows        AnomalyAnalysis.ApiService/Workflows
git mv local.http       AnomalyAnalysis.ApiService/AnomalyAnalysis.ApiService.http
```

- [ ] **Step 2: Write `AnomalyAnalysis.ServiceDefaults/AnomalyAnalysis.ServiceDefaults.csproj`**

Use Template B verbatim, saved to `AnomalyAnalysis/AnomalyAnalysis.ServiceDefaults/AnomalyAnalysis.ServiceDefaults.csproj`.

- [ ] **Step 3: Write `AnomalyAnalysis.ServiceDefaults/Extensions.cs`**

Use Template A verbatim.

- [ ] **Step 4: Write `AnomalyAnalysis.ApiService/AnomalyAnalysis.ApiService.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>AnomalyAnalysis</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\AnomalyAnalysis.ServiceDefaults\AnomalyAnalysis.ServiceDefaults.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Dapr.AspNetCore" Version="1.17.9" />
    <PackageReference Include="Dapr.Client" Version="1.17.9" />
    <PackageReference Include="Dapr.Workflow" Version="1.17.9" />
    <PackageReference Include="Dapr.Workflow.Analyzers" Version="1.17.9" />
    <PackageReference Include="Dapr.AI" Version="1.17.9" />
  </ItemGroup>

</Project>
```

- [ ] **Step 5: Write `AnomalyAnalysis.ApiService/Properties/launchSettings.json`** (Template I verbatim)
- [ ] **Step 6: Write `AnomalyAnalysis.ApiService/appsettings.Development.json`** (Template J verbatim)

- [ ] **Step 7: Edit `AnomalyAnalysis.ApiService/Program.cs` — add two lines**

After `var builder = WebApplication.CreateBuilder(args);` insert `builder.AddServiceDefaults();`. Before `app.Run();` insert `app.MapDefaultEndpoints();`. Same pattern as Task 1 Step 7.

- [ ] **Step 8: Update `AnomalyAnalysis.ApiService/AnomalyAnalysis.ApiService.http`**

Set `@host = http://localhost:5500` for now; the real port is captured in Step 18.

- [ ] **Step 9: Write `AnomalyAnalysis.AppHost/AnomalyAnalysis.AppHost.csproj`**

Generate a fresh GUID with `uuidgen | tr '[:upper:]' '[:lower:]'`. Then write:

```xml
<Project Sdk="Aspire.AppHost.Sdk/13.3.5">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <UserSecretsId>PASTE-GUID-HERE</UserSecretsId>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\AnomalyAnalysis.ApiService\AnomalyAnalysis.ApiService.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Aspire.Hosting.Valkey" Version="13.3.5" />
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

- [ ] **Step 10: Write `AnomalyAnalysis.AppHost/AppHost.cs`**

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
    .WithContainerName("anomaly-analysis-state")
    .WithDataVolume("anomaly-analysis-state-data");

var workflowApp = builder
    .AddProject<Projects.AnomalyAnalysis_ApiService>("wf-app")
    .WithDaprSidecar(new DaprSidecarOptions
    {
        AppId = "anomaly-detection-app",
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

- [ ] **Step 11: Write `AnomalyAnalysis.AppHost/appsettings.json`** (Template F verbatim)
- [ ] **Step 12: Write `AnomalyAnalysis.AppHost/appsettings.Development.json`** (Template G verbatim)

- [ ] **Step 13: Write `AnomalyAnalysis.AppHost/Properties/launchSettings.json`** (Template H with AnomalyAnalysis ports)

```json
{
  "$schema": "https://json.schemastore.org/launchsettings.json",
  "profiles": {
    "http": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "applicationUrl": "https://localhost:15002",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development",
        "DOTNET_ENVIRONMENT": "Development",
        "ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL": "https://localhost:19002",
        "ASPIRE_DASHBOARD_MCP_ENDPOINT_URL": "https://localhost:18102",
        "ASPIRE_RESOURCE_SERVICE_ENDPOINT_URL": "https://localhost:20002"
      }
    }
  }
}
```

- [ ] **Step 14: Write the three Resources YAML files** (Templates C, D, E verbatim)
- [ ] **Step 15: Write `AnomalyAnalysis.sln`**

Use the same .sln template as Task 1 Step 15, substituting `AlienTranslator` → `AnomalyAnalysis` everywhere and generating four fresh GUIDs.

- [ ] **Step 16: Delete obsolete files**

```bash
cd /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems/AnomalyAnalysis
git rm AnomalyAnalysis.csproj
git rm dapr.yaml
git rm -r Properties 2>/dev/null || true
rm -rf bin obj
```

- [ ] **Step 17: Build**

```bash
dotnet build /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems/AnomalyAnalysis/AnomalyAnalysis.sln
```

Expected: 0 errors.

- [ ] **Step 18: Smoke run**

```bash
cd /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems/AnomalyAnalysis
aspire run
```

In a separate terminal, verify `curl -s http://localhost:18080/` reaches the dashboard. Open `https://localhost:15002`, confirm all four resources are running. Capture the ApiService URL from the dashboard. Stop with Ctrl+C.

- [ ] **Step 19: Update `AnomalyAnalysis.ApiService.http`** with the captured ApiService URL (same procedure as Task 1 Step 19).

- [ ] **Step 20: Commit**

```bash
cd /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems
git add AnomalyAnalysis
git commit -m "feat(AnomalyAnalysis): convert to .NET Aspire solution"
```

---

## Task 3: Convert GalacticAnomalyClassifier

| Token | Value |
|---|---|
| `<P>` | `GalacticAnomalyClassifier` |
| `<AppId>` | `anomaly-routing-app` |
| `<ContainerName>` | `galactic-anomaly-classifier-state` |
| `<VolumeName>` | `galactic-anomaly-classifier-state-data` |
| Dashboard port | 15003 |
| OTLP port | 19003 |
| MCP port | 18103 |
| Resource port | 20003 |
| ApiService `Projects.*` type | `Projects.GalacticAnomalyClassifier_ApiService` |

- [ ] **Step 1: Create directories and move source**

```bash
cd /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems/GalacticAnomalyClassifier
mkdir -p GalacticAnomalyClassifier.AppHost/Resources GalacticAnomalyClassifier.AppHost/Properties
mkdir -p GalacticAnomalyClassifier.ApiService/Properties
mkdir -p GalacticAnomalyClassifier.ServiceDefaults
git mv Program.cs       GalacticAnomalyClassifier.ApiService/Program.cs
git mv appsettings.json GalacticAnomalyClassifier.ApiService/appsettings.json
git mv Activities       GalacticAnomalyClassifier.ApiService/Activities
git mv Models           GalacticAnomalyClassifier.ApiService/Models
git mv Workflows        GalacticAnomalyClassifier.ApiService/Workflows
git mv local.http       GalacticAnomalyClassifier.ApiService/GalacticAnomalyClassifier.ApiService.http
```

- [ ] **Step 2:** Write `GalacticAnomalyClassifier.ServiceDefaults/GalacticAnomalyClassifier.ServiceDefaults.csproj` from Template B.
- [ ] **Step 3:** Write `GalacticAnomalyClassifier.ServiceDefaults/Extensions.cs` from Template A.

- [ ] **Step 4: Write `GalacticAnomalyClassifier.ApiService/GalacticAnomalyClassifier.ApiService.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>GalacticAnomalyClassifier</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\GalacticAnomalyClassifier.ServiceDefaults\GalacticAnomalyClassifier.ServiceDefaults.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Dapr.AspNetCore" Version="1.17.9" />
    <PackageReference Include="Dapr.Client" Version="1.17.9" />
    <PackageReference Include="Dapr.Workflow" Version="1.17.9" />
    <PackageReference Include="Dapr.Workflow.Analyzers" Version="1.17.9" />
    <PackageReference Include="Dapr.AI" Version="1.17.9" />
  </ItemGroup>

</Project>
```

- [ ] **Step 5:** Write `GalacticAnomalyClassifier.ApiService/Properties/launchSettings.json` from Template I.
- [ ] **Step 6:** Write `GalacticAnomalyClassifier.ApiService/appsettings.Development.json` from Template J.

- [ ] **Step 7: Edit `GalacticAnomalyClassifier.ApiService/Program.cs`** — same two-line additions as Task 1 Step 7.

- [ ] **Step 8: Update `GalacticAnomalyClassifier.ApiService.http`** — set `@host = http://localhost:5500` placeholder.

- [ ] **Step 9: Write `GalacticAnomalyClassifier.AppHost/GalacticAnomalyClassifier.AppHost.csproj`** — same shape as Task 1 Step 9 with a fresh GUID and `..\GalacticAnomalyClassifier.ApiService\GalacticAnomalyClassifier.ApiService.csproj` as the ProjectReference.

- [ ] **Step 10: Write `GalacticAnomalyClassifier.AppHost/AppHost.cs`**

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
    .WithContainerName("galactic-anomaly-classifier-state")
    .WithDataVolume("galactic-anomaly-classifier-state-data");

var workflowApp = builder
    .AddProject<Projects.GalacticAnomalyClassifier_ApiService>("wf-app")
    .WithDaprSidecar(new DaprSidecarOptions
    {
        AppId = "anomaly-routing-app",
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

- [ ] **Step 11:** Write `GalacticAnomalyClassifier.AppHost/appsettings.json` from Template F.
- [ ] **Step 12:** Write `GalacticAnomalyClassifier.AppHost/appsettings.Development.json` from Template G.

- [ ] **Step 13: Write `GalacticAnomalyClassifier.AppHost/Properties/launchSettings.json`**

```json
{
  "$schema": "https://json.schemastore.org/launchsettings.json",
  "profiles": {
    "http": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "applicationUrl": "https://localhost:15003",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development",
        "DOTNET_ENVIRONMENT": "Development",
        "ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL": "https://localhost:19003",
        "ASPIRE_DASHBOARD_MCP_ENDPOINT_URL": "https://localhost:18103",
        "ASPIRE_RESOURCE_SERVICE_ENDPOINT_URL": "https://localhost:20003"
      }
    }
  }
}
```

- [ ] **Step 14:** Write the three Resources YAML files (Templates C, D, E verbatim).
- [ ] **Step 15:** Write `GalacticAnomalyClassifier.sln` using the Task 1 Step 15 template with project name substituted and four fresh GUIDs.

- [ ] **Step 16: Delete obsolete files**

```bash
cd /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems/GalacticAnomalyClassifier
git rm GalacticAnomalyClassifier.csproj
git rm dapr.yaml
git rm -r Properties 2>/dev/null || true
rm -rf bin obj
```

- [ ] **Step 17: Build**

```bash
dotnet build /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems/GalacticAnomalyClassifier/GalacticAnomalyClassifier.sln
```

- [ ] **Step 18:** `aspire run` from `GalacticAnomalyClassifier/`; verify dashboard at `https://localhost:15003`, diagrid dashboard at `http://localhost:18080`; capture ApiService URL.

- [ ] **Step 19:** Update `GalacticAnomalyClassifier.ApiService.http` with the captured URL.

- [ ] **Step 20: Commit**

```bash
git add GalacticAnomalyClassifier
git commit -m "feat(GalacticAnomalyClassifier): convert to .NET Aspire solution"
```

---

## Task 4: Convert SpaceColonyPlanner

| Token | Value |
|---|---|
| `<P>` | `SpaceColonyPlanner` |
| `<AppId>` | `space-colony-planner-app` |
| `<ContainerName>` | `space-colony-planner-state` |
| `<VolumeName>` | `space-colony-planner-state-data` |
| Dashboard port | 15004 |
| OTLP port | 19004 |
| MCP port | 18104 |
| Resource port | 20004 |
| ApiService `Projects.*` type | `Projects.SpaceColonyPlanner_ApiService` |

Follow Task 2's step-by-step structure (Steps 1-20), substituting `AnomalyAnalysis` → `SpaceColonyPlanner` everywhere, with these specific values that differ from straight substitution:

- [ ] **Step 1:** Same `mkdir`/`git mv` shape as Task 2 Step 1, with `SpaceColonyPlanner` paths.

- [ ] **Step 2–3:** Templates B and A to `SpaceColonyPlanner.ServiceDefaults/`.

- [ ] **Step 4: Write `SpaceColonyPlanner.ApiService/SpaceColonyPlanner.ApiService.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>SpaceColonyPlanner</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\SpaceColonyPlanner.ServiceDefaults\SpaceColonyPlanner.ServiceDefaults.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Dapr.AspNetCore" Version="1.17.9" />
    <PackageReference Include="Dapr.Client" Version="1.17.9" />
    <PackageReference Include="Dapr.Workflow" Version="1.17.9" />
    <PackageReference Include="Dapr.Workflow.Analyzers" Version="1.17.9" />
    <PackageReference Include="Dapr.AI" Version="1.17.9" />
  </ItemGroup>

</Project>
```

- [ ] **Step 5–6:** Templates I and J to `SpaceColonyPlanner.ApiService/`.
- [ ] **Step 7:** Two-line edit to `SpaceColonyPlanner.ApiService/Program.cs`.
- [ ] **Step 8:** Set `@host = http://localhost:5500` in `SpaceColonyPlanner.ApiService.http`.
- [ ] **Step 9:** Write `SpaceColonyPlanner.AppHost/SpaceColonyPlanner.AppHost.csproj` (Task 1 Step 9 template, fresh GUID, `..\SpaceColonyPlanner.ApiService\SpaceColonyPlanner.ApiService.csproj` reference).

- [ ] **Step 10: Write `SpaceColonyPlanner.AppHost/AppHost.cs`**

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
    .WithContainerName("space-colony-planner-state")
    .WithDataVolume("space-colony-planner-state-data");

var workflowApp = builder
    .AddProject<Projects.SpaceColonyPlanner_ApiService>("wf-app")
    .WithDaprSidecar(new DaprSidecarOptions
    {
        AppId = "space-colony-planner-app",
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

- [ ] **Step 11–12:** Templates F and G to `SpaceColonyPlanner.AppHost/`.

- [ ] **Step 13: Write `SpaceColonyPlanner.AppHost/Properties/launchSettings.json`**

```json
{
  "$schema": "https://json.schemastore.org/launchsettings.json",
  "profiles": {
    "http": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "applicationUrl": "https://localhost:15004",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development",
        "DOTNET_ENVIRONMENT": "Development",
        "ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL": "https://localhost:19004",
        "ASPIRE_DASHBOARD_MCP_ENDPOINT_URL": "https://localhost:18104",
        "ASPIRE_RESOURCE_SERVICE_ENDPOINT_URL": "https://localhost:20004"
      }
    }
  }
}
```

- [ ] **Step 14:** Three Resources YAML files (Templates C, D, E).
- [ ] **Step 15:** Write `SpaceColonyPlanner.sln`.

- [ ] **Step 16: Delete obsolete files**

```bash
cd /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems/SpaceColonyPlanner
git rm SpaceColonyPlanner.csproj
git rm dapr.yaml
git rm -r Properties 2>/dev/null || true
rm -rf bin obj
```

- [ ] **Step 17: Build** — `dotnet build SpaceColonyPlanner/SpaceColonyPlanner.sln` from repo root.
- [ ] **Step 18: Smoke run** — `aspire run` from `SpaceColonyPlanner/`, verify dashboard at `https://localhost:15004` and `http://localhost:18080`.
- [ ] **Step 19:** Update `SpaceColonyPlanner.ApiService.http` with captured ApiService URL.

- [ ] **Step 20: Commit**

```bash
git add SpaceColonyPlanner
git commit -m "feat(SpaceColonyPlanner): convert to .NET Aspire solution"
```

---

## Task 5: Convert SpaceDebrisAgent

| Token | Value |
|---|---|
| `<P>` | `SpaceDebrisAgent` |
| `<AppId>` | `space-debris-agent` |
| `<ContainerName>` | `space-debris-agent-state` |
| `<VolumeName>` | `space-debris-agent-state-data` |
| Dashboard port | 15005 |
| OTLP port | 19005 |
| MCP port | 18105 |
| Resource port | 20005 |
| ApiService `Projects.*` type | `Projects.SpaceDebrisAgent_ApiService` |

Follow the same 20-step pattern as Task 4 with `SpaceColonyPlanner` → `SpaceDebrisAgent` everywhere, and the per-project tokens above.

- [ ] **Step 1:** Create directories and move source (paths with `SpaceDebrisAgent`).
- [ ] **Step 2:** Template B → `SpaceDebrisAgent.ServiceDefaults.csproj`.
- [ ] **Step 3:** Template A → `SpaceDebrisAgent.ServiceDefaults/Extensions.cs`.

- [ ] **Step 4: Write `SpaceDebrisAgent.ApiService/SpaceDebrisAgent.ApiService.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>SpaceDebrisAgent</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\SpaceDebrisAgent.ServiceDefaults\SpaceDebrisAgent.ServiceDefaults.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Dapr.AspNetCore" Version="1.17.9" />
    <PackageReference Include="Dapr.Client" Version="1.17.9" />
    <PackageReference Include="Dapr.Workflow" Version="1.17.9" />
    <PackageReference Include="Dapr.Workflow.Analyzers" Version="1.17.9" />
    <PackageReference Include="Dapr.AI" Version="1.17.9" />
  </ItemGroup>

</Project>
```

- [ ] **Step 5–6:** Templates I and J to `SpaceDebrisAgent.ApiService/`.
- [ ] **Step 7:** Two-line edit to `SpaceDebrisAgent.ApiService/Program.cs`.
- [ ] **Step 8:** `@host = http://localhost:5500` placeholder in `SpaceDebrisAgent.ApiService.http`.
- [ ] **Step 9:** AppHost csproj for `SpaceDebrisAgent` (fresh GUID, reference to ApiService csproj).

- [ ] **Step 10: Write `SpaceDebrisAgent.AppHost/AppHost.cs`**

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
    .WithContainerName("space-debris-agent-state")
    .WithDataVolume("space-debris-agent-state-data");

var workflowApp = builder
    .AddProject<Projects.SpaceDebrisAgent_ApiService>("wf-app")
    .WithDaprSidecar(new DaprSidecarOptions
    {
        AppId = "space-debris-agent",
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

- [ ] **Step 11–12:** Templates F and G to `SpaceDebrisAgent.AppHost/`.

- [ ] **Step 13: Write `SpaceDebrisAgent.AppHost/Properties/launchSettings.json`**

```json
{
  "$schema": "https://json.schemastore.org/launchsettings.json",
  "profiles": {
    "http": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "applicationUrl": "https://localhost:15005",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development",
        "DOTNET_ENVIRONMENT": "Development",
        "ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL": "https://localhost:19005",
        "ASPIRE_DASHBOARD_MCP_ENDPOINT_URL": "https://localhost:18105",
        "ASPIRE_RESOURCE_SERVICE_ENDPOINT_URL": "https://localhost:20005"
      }
    }
  }
}
```

- [ ] **Step 14:** Three Resources YAML files (Templates C, D, E).
- [ ] **Step 15:** Write `SpaceDebrisAgent.sln`.

- [ ] **Step 16: Delete obsolete files**

```bash
cd /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems/SpaceDebrisAgent
git rm SpaceDebrisAgent.csproj
git rm dapr.yaml
git rm -r Properties 2>/dev/null || true
rm -rf bin obj
```

- [ ] **Step 17: Build** — `dotnet build SpaceDebrisAgent/SpaceDebrisAgent.sln`.
- [ ] **Step 18: Smoke run** — verify dashboard at `https://localhost:15005` and `http://localhost:18080`.
- [ ] **Step 19:** Update `SpaceDebrisAgent.ApiService.http`.

- [ ] **Step 20: Commit**

```bash
git add SpaceDebrisAgent
git commit -m "feat(SpaceDebrisAgent): convert to .NET Aspire solution"
```

---

## Task 6: Convert StarshipDiagnostics

| Token | Value |
|---|---|
| `<P>` | `StarshipDiagnostics` |
| `<AppId>` | `starship-diagnostics-app` |
| `<ContainerName>` | `starship-diagnostics-state` |
| `<VolumeName>` | `starship-diagnostics-state-data` |
| Dashboard port | 15006 |
| OTLP port | 19006 |
| MCP port | 18106 |
| Resource port | 20006 |
| ApiService `Projects.*` type | `Projects.StarshipDiagnostics_ApiService` |

- [ ] **Step 1:** Create directories and move source (paths with `StarshipDiagnostics`).
- [ ] **Step 2:** Template B → `StarshipDiagnostics.ServiceDefaults.csproj`.
- [ ] **Step 3:** Template A → `StarshipDiagnostics.ServiceDefaults/Extensions.cs`.

- [ ] **Step 4: Write `StarshipDiagnostics.ApiService/StarshipDiagnostics.ApiService.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>StarshipDiagnostics</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\StarshipDiagnostics.ServiceDefaults\StarshipDiagnostics.ServiceDefaults.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Dapr.AspNetCore" Version="1.17.9" />
    <PackageReference Include="Dapr.Client" Version="1.17.9" />
    <PackageReference Include="Dapr.Workflow" Version="1.17.9" />
    <PackageReference Include="Dapr.Workflow.Analyzers" Version="1.17.9" />
    <PackageReference Include="Dapr.AI" Version="1.17.9" />
  </ItemGroup>

</Project>
```

- [ ] **Step 5–6:** Templates I and J to `StarshipDiagnostics.ApiService/`.
- [ ] **Step 7:** Two-line edit to `StarshipDiagnostics.ApiService/Program.cs`.
- [ ] **Step 8:** `@host = http://localhost:5500` placeholder in `StarshipDiagnostics.ApiService.http`.
- [ ] **Step 9:** AppHost csproj for `StarshipDiagnostics`.

- [ ] **Step 10: Write `StarshipDiagnostics.AppHost/AppHost.cs`**

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
    .WithContainerName("starship-diagnostics-state")
    .WithDataVolume("starship-diagnostics-state-data");

var workflowApp = builder
    .AddProject<Projects.StarshipDiagnostics_ApiService>("wf-app")
    .WithDaprSidecar(new DaprSidecarOptions
    {
        AppId = "starship-diagnostics-app",
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

- [ ] **Step 11–12:** Templates F and G to `StarshipDiagnostics.AppHost/`.

- [ ] **Step 13: Write `StarshipDiagnostics.AppHost/Properties/launchSettings.json`**

```json
{
  "$schema": "https://json.schemastore.org/launchsettings.json",
  "profiles": {
    "http": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "applicationUrl": "https://localhost:15006",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development",
        "DOTNET_ENVIRONMENT": "Development",
        "ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL": "https://localhost:19006",
        "ASPIRE_DASHBOARD_MCP_ENDPOINT_URL": "https://localhost:18106",
        "ASPIRE_RESOURCE_SERVICE_ENDPOINT_URL": "https://localhost:20006"
      }
    }
  }
}
```

- [ ] **Step 14:** Three Resources YAML files (Templates C, D, E).
- [ ] **Step 15:** Write `StarshipDiagnostics.sln`.

- [ ] **Step 16: Delete obsolete files**

```bash
cd /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems/StarshipDiagnostics
git rm StarshipDiagnostics.csproj
git rm dapr.yaml
git rm -r Properties 2>/dev/null || true
rm -rf bin obj
```

- [ ] **Step 17: Build** — `dotnet build StarshipDiagnostics/StarshipDiagnostics.sln`.
- [ ] **Step 18: Smoke run** — verify dashboard at `https://localhost:15006` and `http://localhost:18080`.
- [ ] **Step 19:** Update `StarshipDiagnostics.ApiService.http`.

- [ ] **Step 20: Commit**

```bash
git add StarshipDiagnostics
git commit -m "feat(StarshipDiagnostics): convert to .NET Aspire solution"
```

---

## Task 7: Delete the obsolete repo-root .sln and Resources/ folder

**Files:**
- Delete: `dapr-reliable-agentic-systems.sln`
- Delete: `Resources/statestore.yaml`
- Delete: `Resources/conversation.yaml`
- Delete: `Resources/` (directory)

- [ ] **Step 1: Confirm all six per-project solutions exist**

```bash
cd /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems
ls AlienTranslator/AlienTranslator.sln \
   AnomalyAnalysis/AnomalyAnalysis.sln \
   GalacticAnomalyClassifier/GalacticAnomalyClassifier.sln \
   SpaceColonyPlanner/SpaceColonyPlanner.sln \
   SpaceDebrisAgent/SpaceDebrisAgent.sln \
   StarshipDiagnostics/StarshipDiagnostics.sln
```

Expected: all six listed without error. If any is missing, return to the corresponding Task 1–6 and complete it first.

- [ ] **Step 2: Delete root .sln and root Resources/**

```bash
git rm dapr-reliable-agentic-systems.sln
git rm -r Resources
```

Expected: `git status` shows three deletions (the .sln plus both YAML files inside Resources/).

- [ ] **Step 3: Build all six solutions to confirm none depend on the deleted files**

```bash
for sln in AlienTranslator/AlienTranslator.sln \
           AnomalyAnalysis/AnomalyAnalysis.sln \
           GalacticAnomalyClassifier/GalacticAnomalyClassifier.sln \
           SpaceColonyPlanner/SpaceColonyPlanner.sln \
           SpaceDebrisAgent/SpaceDebrisAgent.sln \
           StarshipDiagnostics/StarshipDiagnostics.sln; do
  echo "=== $sln ==="
  dotnet build "$sln" || { echo "BUILD FAILED: $sln"; exit 1; }
done
```

Expected: every build succeeds. If any fails, investigate (likely a forgotten reference to the root `Resources/` path inside an AppHost.cs — fix and rebuild).

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "chore: remove obsolete root solution and shared Resources folder"
```

---

## Task 8: Update `AGENTS.md` to reflect new conventions

**Files:**
- Modify: `AGENTS.md`

- [ ] **Step 1: Replace the back-end development rules section**

In `AGENTS.md`, locate the section starting with `# Back-end development rules`. Replace the entire bulleted list under that heading (everything from the first bullet to the last bullet of that section) with this exact content:

```markdown
# Back-end development rules
- Each project is a .NET Aspire solution composed of three csprojs: `<Name>.AppHost` (`Aspire.AppHost.Sdk` 13.3.5, net10.0), `<Name>.ApiService` (`Microsoft.NET.Sdk.Web`, net10.0), `<Name>.ServiceDefaults` (`Microsoft.NET.Sdk`, net10.0).
- The ApiService targets net10.0 and uses Dapr packages at version 1.17.9 (`Dapr.AspNetCore`, `Dapr.Client`, `Dapr.Workflow`, `Dapr.Workflow.Analyzers`, `Dapr.AI`).
- The AppHost orchestrates Valkey (port 16379, password-protected), the ApiService with a Dapr sidecar (state store component name `statestore`), and a `diagrid-dashboard` container on port 18080. Dapr components live in `<Name>.AppHost/Resources/`.
- Run a solution with `aspire run` from the solution root. Do not use `dapr run` or `dapr.yaml` — Aspire owns the sidecar lifecycle.
- `Program.cs` calls `builder.AddServiceDefaults()` before service registration and `app.MapDefaultEndpoints()` before `app.Run()`.
- Code is written in C# using ASP.NET Core minimal API style.
- Keep code small and modular. Do not introduce unnecessary new classes or files.
- Dapr Workflow is used for orchestrating business logic and orchestration across services.
- The Program.cs file for the workflow application contains a `start` POST endpoint that uses the DaprWorkflowClient to start a new workflow instance. It also contains a `get` GET endpoint to retrieve the status of a workflow instance by its ID.
- For each HTTP endpoint in the Program.cs, a corresponding endpoint is added in a `<Name>.ApiService.http` file that the VSCode REST client can use.
- Do not comment every class or method. Only add comments where calculations are made or where the logic is complex.
```

The `# Role`, `# Git rules`, and `# Front-end development rules` sections remain unchanged.

- [ ] **Step 2: Verify the diff**

```bash
git diff AGENTS.md
```

Expected: the three bullets pinning .NET 9 / Dapr 1.16.1 / Dapr.Workflow.Analyzers are removed and replaced with the five new bullets describing the Aspire layout, Dapr 1.17.9, AppHost orchestration, `aspire run`, and the AddServiceDefaults/MapDefaultEndpoints conventions. The five preserved C#/ASP.NET bullets remain.

- [ ] **Step 3: Commit**

```bash
git add AGENTS.md
git commit -m "docs(AGENTS): align conventions with .NET 10 Aspire layout"
```

---

## Task 9: Update per-project READMEs

**Files (one Modify per project):**
- Modify: `AlienTranslator/README.md`
- Modify: `AnomalyAnalysis/README.md`
- Modify: `GalacticAnomalyClassifier/README.md`
- Modify: `SpaceColonyPlanner/README.md`
- Modify: `SpaceDebrisAgent/README.md`
- Modify: `StarshipDiagnostics/README.md`

Each README has a "Running the application" (or similarly-named) section that currently instructs users to run `dapr run -f dapr.yaml`. Replace that section with the Aspire equivalent. Architecture, workflow diagrams, API examples, and sample payloads in each README are preserved verbatim.

- [ ] **Step 1: For each of the six README files, replace the "Running" section**

For `AlienTranslator/README.md` (repeat the same edit for the other five, substituting the project name):

Find the existing "Running" or "How to run" or "Getting started" section that references `dapr run -f dapr.yaml`. Replace that entire section with the markdown content below (the four-backtick fence is just to display the markdown verbatim — write only the inner content to the README, without the four-backtick wrapper):

````markdown
## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download)
- [Aspire CLI](https://aspire.dev/get-started/install-cli/) — install with `dotnet tool install -g Aspire.Cli`
- [Docker](https://www.docker.com/products/docker-desktop/) or [Podman](https://podman.io/docs/installation)
- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/) (version 1.17+)
- [Ollama](https://ollama.com/) with the `llama3.2:3b` model pulled (`ollama pull llama3.2:3b`)

## Running the application

From the `AlienTranslator/` folder:

```shell
aspire run
```

This launches the Aspire AppHost, which orchestrates:
- A Valkey container for workflow state persistence (port 16379, password-protected)
- The ApiService with a Dapr sidecar (app ID `alien-translator-app`)
- The Diagrid Dev Dashboard container on http://localhost:18080

The Aspire dashboard opens automatically in the browser, showing all resources and their status. From there you can click the ApiService endpoint to discover its assigned HTTP port, and use the requests in `AlienTranslator.ApiService/AlienTranslator.ApiService.http` to exercise the workflow.

## Inspecting workflow execution

The Diagrid Dev Dashboard is managed by Aspire and runs as a container resource on http://localhost:18080. Use it to browse workflow instances, view their status, and inspect execution history.
````

For each of the other five READMEs, substitute the project name and AppId in the relevant lines:

| Project | AppId line | Folder line |
|---|---|---|
| AnomalyAnalysis | `app ID anomaly-detection-app` | `From the AnomalyAnalysis/ folder` |
| GalacticAnomalyClassifier | `app ID anomaly-routing-app` | `From the GalacticAnomalyClassifier/ folder` |
| SpaceColonyPlanner | `app ID space-colony-planner-app` | `From the SpaceColonyPlanner/ folder` |
| SpaceDebrisAgent | `app ID space-debris-agent` | `From the SpaceDebrisAgent/ folder` |
| StarshipDiagnostics | `app ID starship-diagnostics-app` | `From the StarshipDiagnostics/ folder` |

Also update the `AlienTranslator.ApiService.http` reference at the end of the "Running" section to use the correct project's `.http` filename.

- [ ] **Step 2: Spot-check one updated README renders correctly**

Run:
```bash
grep -A 5 "aspire run" /Users/marcduiker/dev/diagrid-labs/dapr-reliable-agentic-systems/AlienTranslator/README.md
```

Expected: returns the `aspire run` block. If it returns nothing, the edit didn't land — re-check the section heading match.

- [ ] **Step 3: Commit**

```bash
git add AlienTranslator/README.md AnomalyAnalysis/README.md GalacticAnomalyClassifier/README.md \
        SpaceColonyPlanner/README.md SpaceDebrisAgent/README.md StarshipDiagnostics/README.md
git commit -m "docs(readme): switch run instructions to aspire run"
```

---

## Done

After Task 9, the repository contains six independent .NET Aspire solutions, an updated `AGENTS.md`, six updated per-project READMEs, and no traces of the old root `.sln` or `Resources/` folder. The `ConversationTests/` project is untouched. Each solution can be run independently with `aspire run` from its own folder, exposing the Diagrid Dev Dashboard on http://localhost:18080.

**Recommended final check (manual, not a separate task):** run `aspire run` once from each of the six solution folders in sequence, hit one workflow endpoint per solution from its `.http` file, and confirm the workflow instance shows up in the Diagrid dashboard.
