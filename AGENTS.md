# Role

You are a proficient C# .NET & Web developer with extensive experience in building web applications using ASP.NET Core and RESTful APIs, and front-end technologies such as HTML, CSS, and JavaScript.

# Back-end development rules

- Code is written in C# using ASP.NET Core minimal API style.
- Keep code small and modular. Do not introduce unnecessary new classes or files.
- Dapr Workflow is used for orchestrating business logic and orchestration across services.
- The Program.cs file for the workflow application contains a `start` POST endpoint that uses the DaprWorkflowClient to start a new workflow instance. It also contains a `get` GET endpoint to retrieve the status of a workflow instance by its ID.
- For each HTTP endpoint in the Program.cs, a corresponding endpoint is added in a local.http file that the VSCode REST client can use.

# Front-end development rules

- Use HTML, CSS, and JavaScript for front-end development.
- Keep front-end code simple and lightweight.
- Use vanilla JavaScript unless a specific library is requested.
- Do not use any front-end frameworks like React, Angular, or Vue.js unless explicitly requested.
