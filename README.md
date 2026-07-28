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
- Devices send authenticated updates to `POST /api/devices/{deviceId}/updates` using `X-Api-Key`.
- Each device update refreshes the latest snapshot on the device record and appends temperature and position history rows for later trend views.
- Admin cleanup operations are available for deleting a device or clearing temperature/position history independently.

## Device Details, Config Sync, And Logs

- The device inventory page keeps unregistered devices first and refreshes itself with visibility-aware polling.
- Each device has a route-backed details page at `/devices/:id` instead of the old modal flow.
- Registered devices can update `name`, `place`, and `reportIntervalSeconds` through `PUT /api/devices/{id}`.
- The backend now tracks both desired configuration and the runtime configuration last reported by the device.
- Configuration sync status is version-based:
	- `DesiredConfigurationVersion` increments when device-consumable settings change.
	- The device reports its applied configuration version and applied report interval in each authenticated update.
	- The frontend shows whether configuration is still pending on the device.
- Device updates remain a single authenticated write call at `POST /api/devices/{deviceId}/updates`, now carrying:
  - telemetry
  - runtime configuration
  - a batch of unsent device log lines
- The ESP32 stores unsent logs in segmented LittleFS files. Segments survive restarts and are removed only after the backend acknowledges their highest sequence number.
- ESP32 log sequence numbers are reserved persistently in blocks and start above the legacy in-memory range, so a restart cannot reuse a sequence number that is already stored by the backend.
- Device logs are stored indefinitely server-side with an idempotency key of `(DeviceId, SequenceNumber)` so retried uploads do not create duplicates.
- `GET /api/devices/{id}/logs` supports newest-first cursor reads plus server-side `search`, `level`, `fromUtc`, and `toUtc` filters. The frontend polls with `afterId` for live lines and uses `beforeId` to load older matching history.
- `DELETE /api/devices/{id}/logs` removes every log for a device. Supplying `beforeUtc` removes only entries older than that cutoff.
- The log tab exposes full-history search, live updates, older-history loading, delete-all, and retention controls for keeping the last hour, 24 hours, week, or a custom number of days.

The backend has no configured log row limit and PostgreSQL can retain millions of entries. The ESP32 queue is no longer capped at 120 RAM entries, but it is still bounded by the board's physical LittleFS flash capacity. If flash cannot accept another record, the firmware reports the write failure on Serial and does not silently discard an older queued record.

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
