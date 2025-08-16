# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY WaterTemperature.Api/WaterTemperature.Api.csproj WaterTemperature.Api/
RUN dotnet restore WaterTemperature.Api/WaterTemperature.Api.csproj
COPY . .
RUN dotnet publish WaterTemperature.Api/WaterTemperature.Api.csproj -c Release -o /out /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /out .
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
ENTRYPOINT ["dotnet", "WaterTemperature.Api.dll"]
