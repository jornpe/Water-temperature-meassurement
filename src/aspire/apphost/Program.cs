var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
	.AddDatabase("watertemp");

// Backend API (ASP.NET Core project)
var api = builder.AddProject("api", "../../backend/WaterTemperature.Api/WaterTemperature.Api.csproj")
	.WithHttpEndpoint(port: 8080)
	.WithReference(postgres)
	.WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
	.WaitFor(postgres);

// Frontend (Vite) running inside a Node container with a bind mount for live dev
// Use a dynamic host port to avoid conflicts that would cause the dev server/container to exit.
var frontend = builder.AddContainer("frontend", "node:20-alpine")
	.WithBindMount("../../frontend/app", "/workspace")
	.WithEnvironment("CI", "true")
	.WithEnvironment("API_PROXY_TARGET", "http://host.docker.internal:8080")
	.WithHttpEndpoint(targetPort: 5173)
	.WithReference(api)
	.WithEntrypoint("sh")
	.WithArgs("-lc", "cd /workspace && npm ci && npm run dev -- --host 0.0.0.0 --port 5173")
	.WaitFor(api);

builder.Build().Run();
