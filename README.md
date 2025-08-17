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

## Database & Auth

- Postgres is included as `db` in Compose (16-alpine), with a named volume `db-data`.
- The backend uses EF Core with Npgsql. On dev Compose, `Database__AutoCreate=true` bootstraps the schema.
- Local user auth is built-in with a single admin user stored in the DB:
	- First-time: frontend will show Register when no users exist; creates the only admin.
	- Subsequent sessions: Login page issues a JWT for API access.
	- Backend enforces single-user on `/api/auth/register` (409 when a user exists).

### Important environment variables

- Backend
	- `ConnectionStrings__Default` e.g. `Host=db;Port=5432;Database=watertemp;Username=app;Password=...`
	- `JWT__Secret` secret for signing JWTs (set a strong value in prod)
	- `Database__AutoCreate` set `true` to auto-create schema (dev only)
- Frontend
	- Dev proxy: `API_PROXY_TARGET` (Vite) default `http://localhost:8080`
	- Container: `API_BASE_URL` for nginx to proxy `/api` at runtime (default `http://backend:8080`)

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
