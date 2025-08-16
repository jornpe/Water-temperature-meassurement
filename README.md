# Water Temperature Measurement — Monorepo Overview

This repository is a small monorepo with a .NET backend API and a React + Vite frontend, plus optional .NET Aspire AppHost wiring and an ESP32 placeholder.

## Layout

- `src/backend/WaterTemperature.Api` — ASP.NET minimal API
	- Exposes REST endpoints under `/api/*`
	- Listens on port `8080` in Docker
	- Tests in `src/backend/WaterTemperature.Api.Tests`

- `src/frontend/app` — React + Vite (TypeScript)
	- Dev: Vite proxies `/api` → `http://localhost:8080` (or `API_PROXY_TARGET`)
	- Container/Prod: served by nginx on port `80`; reverse proxy to backend via `API_BASE_URL`
	- Includes `nginx.conf.template` and `docker-entrypoint.sh` for runtime config

- `src/aspire/apphost` — .NET Aspire AppHost (optional during dev)
	- Central place to compose services during local development

- `src/esp32/` — PlatformIO project placeholder for device-side code

## Running & Building (quick pointers)

- Backend
	- Local build: use the provided VS Code task “Build .NET backend”
	- Tests: “Run backend tests” (xUnit)
	- Docker image exposes `8080`

- Frontend
	- Local dev: Vite dev server; proxies `/api` (configure with `API_PROXY_TARGET` if needed)
	- Build: “Build frontend”; container serves on `80` via nginx

- Compose
	- `docker-compose.yml` for local composition
	- `docker-compose.prod.yml` for production-like composition

## Configuration Contracts

- API base path: all backend endpoints under `/api/*`
- Frontend → Backend
	- Dev: Vite proxy (default `http://localhost:8080`, override with `API_PROXY_TARGET`)
	- Containers/Prod: set `API_BASE_URL` for the nginx reverse proxy

## Notes

- Dockerfiles live in `src/backend/WaterTemperature.Api/Dockerfile` and `src/frontend/app/Dockerfile`.
- Ports: Backend `8080`, Frontend `80` when containerized.
# Water temperature meassurement
Codebase for a device that registers water temperatures, then a backend that handles the data and pushes this to yr.no
