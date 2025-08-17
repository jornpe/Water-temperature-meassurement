var builder = DistributedApplication.CreateBuilder(args);

// Backend API (ASP.NET Core project)
var api = builder.AddProject("api", "../../backend/WaterTemperature.Api/WaterTemperature.Api.csproj")
	.WithHttpEndpoint(port: 8080);

// PostgreSQL database for the API
var db = builder.AddContainer("db", "postgres:16-alpine")
	.WithEnvironment("POSTGRES_DB", "watertemp")
	.WithEnvironment("POSTGRES_USER", "app")
	.WithEnvironment("POSTGRES_PASSWORD", "appsecret")
	// Expose Postgres on host to allow the API (running on host) to connect (use 5433 to avoid conflicts)
	.WithEndpoint(port: 5433, targetPort: 5432);

// Provide connection string to the API
// Point to the host-mapped Postgres port
api.WithEnvironment("ConnectionStrings__Default", "Host=localhost;Port=5433;Database=watertemp;Username=app;Password=appsecret;Pooling=true;KeepAlive=30");
api.WithEnvironment("JWT__Secret", "dev-super-secret-change-me");
api.WithEnvironment("Database__AutoCreate", "true");

// Frontend (Vite) running inside a Node container with a bind mount for live dev
// Use a dynamic host port to avoid conflicts that would cause the dev server/container to exit.
var frontend = builder.AddContainer("frontend", "node:20-alpine")
	.WithBindMount("../../frontend/app", "/workspace")
	.WithEnvironment("CI", "true")
	.WithEnvironment("API_PROXY_TARGET", "http://host.docker.internal:8080")
	.WithHttpEndpoint(targetPort: 5173)
	.WithReference(api)
	.WithEntrypoint("sh")
	.WithArgs("-lc", "cd /workspace && npm ci && npm run dev -- --host 0.0.0.0 --port 5173");

builder.Build().Run();
