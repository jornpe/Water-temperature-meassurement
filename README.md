# Water Temperature Measurement — Monorepo Overview

This repository is a small monorepo with a .NET backend API and a React + Vite frontend, plus optional .NET Aspire AppHost wiring and an ESP32 device project.

## Layout

- `src/backend/WaterTemperature.Api` — ASP.NET Web API
	- Exposes REST endpoints under `/api/*`
	- Uses controller-based endpoints with JWT auth for admin users
	- Persists devices, latest telemetry snapshots, and temperature/position history with EF Core + PostgreSQL
	- Listens on port `8080` in Docker
	- Tests in `src/backend/WaterTemperature.Api.Tests`

- `src/frontend/app` — React + Vite (TypeScript)
	- Dev: Vite proxies `/api` → `http://localhost:8080` (or `API_PROXY_TARGET`)
	- Container/Prod: served by nginx on port `80`; reverse proxy to backend via `API_BASE_URL`
	- The main Sensors route is a device inventory view backed by `/api/devices`
	- Device cards open a dedicated `/devices/:id` page with sensor, configuration, and log tabs
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
- The backend uses EF Core with Npgsql and applies pending migrations during startup.
- Local user auth is built-in with a single admin user stored in the DB:
	- First-time: frontend will show Register when no users exist; creates the only admin.
	- Subsequent sessions: Login page issues a JWT for API access.
	- Backend enforces single-user on `/api/auth/register` (409 when a user exists).

## Device Onboarding Flow

- Devices call `POST /api/devices/discover` without credentials using their stable hardware-derived device ID.
- The backend creates one unregistered device record per unique device ID and updates discovery timestamps on later discovery calls.
- The frontend device inventory shows unregistered devices first and lets an authenticated admin register them with name, place, and report interval.
- Registration generates a hashed device API key server-side. The plaintext key is only exposed back to the device through the pending discovery handoff path.
- Devices authenticate configuration syncs and telemetry updates using `X-Api-Key`.
- Devices synchronize runtime configuration through `POST /api/devices/{deviceId}/configuration`.
- Devices send telemetry and logs through `POST /api/devices/{deviceId}/updates`.
- Each device update refreshes the latest snapshot on the device record and appends temperature and position history rows for later trend views.
- Admin cleanup operations are available for deleting a device or clearing temperature/position history independently.

## Device Details, Config Sync, And Logs

- The device inventory page keeps unregistered devices first and refreshes itself with visibility-aware polling.
- Each device has a route-backed details page at `/devices/:id` instead of the old modal flow.
- Registered devices can update `name`, `place`, and `reportIntervalSeconds` through `PUT /api/devices/{id}`.
- The backend now tracks both desired configuration and the runtime configuration last reported by the device.
- Configuration sync status is version-based:
	- `DesiredConfigurationVersion` increments when device-consumable settings change.
	- The device reports its applied configuration version and report interval to `POST /api/devices/{deviceId}/configuration` and receives the desired configuration in the response.
	- After applying a change, the device reads the configuration back from Preferences, verifies that the stored and in-memory values match, and sends a confirmation to the configuration endpoint.
	- The backend independently compares the confirmed version and interval with the desired configuration. Only an exact match with successful device-side storage verification is marked synchronized.
	- The device retries the complete apply/read-back/confirm cycle up to three times. A third failed confirmation is persisted as a configuration-sync failure.
	- The frontend distinguishes pending, synchronized, and failed configuration states and shows the final verification error after retries are exhausted.
- Device updates at `POST /api/devices/{deviceId}/updates` carry telemetry and a batch of unsent device log lines. Configuration is not included in either the update request or response.
- The ESP32 keeps up to 25 unsent log lines in memory. A successful update removes the uploaded batch; a restart intentionally discards anything still queued.
- Each log carries its `millis()` timestamp and each update carries the current device uptime. The backend converts those values to a UTC log timestamp and uses it for ordering and time filters.
- `GET /api/devices/{id}/logs` supports newest-first cursor reads plus server-side `search`, `level`, `fromUtc`, and `toUtc` filters. The frontend polls with `afterId` for live lines and uses `beforeId` to load older matching history.
- `DELETE /api/devices/{id}/logs` removes every log for a device. Supplying `beforeUtc` removes only entries older than that cutoff.
- The log tab exposes full-history search, live updates, older-history loading, delete-all, and retention controls for keeping the last hour, 24 hours, week, or a custom number of days.

The backend has no configured log row limit and PostgreSQL can retain millions of entries. If the ESP32's 25-entry RAM queue fills before an upload succeeds, the oldest queued line is discarded.

## Migration Workflow

- Create schema changes with EF Core migrations in `src/backend/WaterTemperature.Api/Migrations`.
- Do not run manual database update commands against environments. Start the backend and let its existing startup path apply pending migrations.
- The device onboarding schema is introduced by the `DeviceOnboardingFoundation` migration.
- The route-backed device details, configuration-sync state, and device log storage are introduced by the `DeviceDetailsAndLogging` migration.

### Important environment variables

- Backend
	- `ConnectionStrings__watertemp` e.g. `Host=db;Port=5432;Database=watertemp;Username=app;Password=...`
	- `JwtSettings__Secret` secret for signing JWTs (set a strong value in prod)
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
- Home Assistant publishing is not device-direct in the current backend/frontend slice; device onboarding and telemetry now center on backend-owned device records.
# Water temperature meassurement
Codebase for a device that registers water temperatures, then a backend that handles the data and pushes this to yr.no
