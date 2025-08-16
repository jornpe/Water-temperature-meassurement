var builder = DistributedApplication.CreateBuilder(args);

// Backend API (ASP.NET Core project)
var api = builder.AddProject("api", "../../backend/WaterTemperature.Api/WaterTemperature.Api.csproj")
	.WithHttpEndpoint(port: 8080);

// Frontend (Vite) running inside a Node container with a bind mount for live dev
var frontend = builder.AddContainer("frontend", "node:20-alpine")
	.WithBindMount("../../frontend/app", "/workspace")
	.WithEnvironment("CI", "true")
	.WithEnvironment("API_PROXY_TARGET", "http://host.docker.internal:8080")
	.WithHttpEndpoint(port: 5173, targetPort: 5173)
	.WithReference(api)
	.WithEntrypoint("sh")
	.WithArgs("-lc", "cd /workspace && npm ci && npm run dev -- --host 0.0.0.0 --port 5173 --strictPort");

builder.Build().Run();
