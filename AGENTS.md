# Role

You are a proficient C# .NET & Web developer with extensive experience in building web applications using ASP.NET Core and RESTful APIs, and front-end technologies such as HTML, CSS, and JavaScript.

# Git rules
- Ensure a `.gitattributes file exists with the following lines:
  ```
  * text=auto eol=lf
  *.sh  text eol=lf
  *.bat text eol=crlf
  *.cmd text eol=crlf
  ```

# Back-end development rules
- Each project is a .NET Aspire solution composed of three csprojs: `<Name>.AppHost` (`Aspire.AppHost.Sdk` 13.6.1, net10.0), `<Name>.ApiService` (`Microsoft.NET.Sdk.Web`, net10.0), `<Name>.ServiceDefaults` (`Microsoft.NET.Sdk`, net10.0). A project can add an optional `<Name>.Web` project (`Microsoft.NET.Sdk.Web`, net10.0) that serves a vanilla JS front-end and proxies `/api/*` to the ApiService with YARP (all six demos have one). Each demo's front-end is self-contained: copies of the front-end files live in each project, duplication across the Aspire projects is intended, and nothing is shared between them.
- The ApiService targets net10.0 and uses Dapr packages at version 1.18.10 (`Dapr.AspNetCore`, `Dapr.Client`, `Dapr.Workflow`, `Dapr.AI`). The workflow analyzers are built into `Dapr.Workflow`, so `Dapr.Workflow.Analyzers` is not referenced.
- The AppHost orchestrates Valkey (port 16379, password-protected), the ApiService with a Dapr sidecar (state store component name `statestore`), and the locally installed Diagrid Dapr Dev Dashboard as a `dapr-dev-dashboard` executable resource on port 9090. Dapr components live in `<Name>.AppHost/Resources/`.
- `AddDaprWorkflow()` auto-registers workflows and activities; `AddDaprWorkflowVersioning()` is also called. Do not add `RegisterWorkflow`/`RegisterActivity` calls.
- Run a solution with `aspire run` from the solution root. Do not use `dapr run` or `dapr.yaml` — Aspire owns the sidecar lifecycle.
- `Program.cs` calls `builder.AddServiceDefaults()` before service registration and `app.MapDefaultEndpoints()` before `app.Run()`.
- Code is written in C# using ASP.NET Core minimal API style.
- Keep code small and modular. Do not introduce unnecessary new classes or files.
- Dapr Workflow is used for orchestrating business logic and orchestration across services.
- The Program.cs file for the workflow application contains a `start` POST endpoint that uses the DaprWorkflowClient to start a new workflow instance. It also contains a `get` GET endpoint to retrieve the status of a workflow instance by its ID.
- For each HTTP endpoint in the Program.cs, a corresponding endpoint is added in a `<Name>.ApiService.http` file that the VSCode REST client can use.
- Do not comment every class or method. Only add comments where calculations are made or where the logic is complex.

# Front-end development rules

- Use HTML, CSS, and JavaScript for front-end development.
- Keep front-end code simple and lightweight.
- Use vanilla JavaScript unless a specific library is requested.
- Do not use any front-end frameworks like React, Angular, or Vue.js unless explicitly requested.
