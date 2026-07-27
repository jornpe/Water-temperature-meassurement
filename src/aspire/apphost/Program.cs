var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
	.AddDatabase("watertemp");

// Backend API (ASP.NET Core project)
var api = builder.AddProject("api", "../../backend/WaterTemperature.Api/WaterTemperature.Api.csproj")
	.WithHttpEndpoint(port: 8080)
	.WithReference(postgres)
	.WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
	.WaitFor(postgres)
	.WithHttpHealthCheck("/health");

// Run the Vite development server directly. The JavaScript hosting integration
// manages package installation and endpoint/port configuration.
var frontend = builder.AddViteApp("frontend", "../../frontend/app")
	.WithEnvironment("API_PROXY_TARGET", api.GetEndpoint("http"))
	.WithReference(api)
	.WaitFor(api);

builder.Build().Run();
