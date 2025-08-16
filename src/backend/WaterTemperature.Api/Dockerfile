# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
# Copy project files needed for restore (use repo root as build context)
COPY src/backend/WaterTemperature.Api/WaterTemperature.Api.csproj src/backend/WaterTemperature.Api/
COPY src/aspire/servicedefaults/servicedefaults.csproj src/aspire/servicedefaults/
RUN dotnet restore src/backend/WaterTemperature.Api/WaterTemperature.Api.csproj
# Copy the rest of the source
COPY . .
RUN dotnet publish src/backend/WaterTemperature.Api/WaterTemperature.Api.csproj -c Release -o /out /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /out .
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
ENTRYPOINT ["dotnet", "WaterTemperature.Api.dll"]
