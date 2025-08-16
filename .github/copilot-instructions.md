Coding conventions and project map

- Monorepo layout under `src/`
  - `backend/WaterTemperature.Api` — ASP.NET 8 minimal API
  - `frontend/app` — React + Vite app
  - `aspire/AppHost` — .NET Aspire AppHost (optional during dev)
  - `esp32/` — PlatformIO project placeholder
- Prefer TypeScript on the frontend; minimal API endpoints under `/api/*`.
- Frontend reaches backend via:
  - Dev (Vite): proxy `/api` to `http://localhost:8080` or env `API_PROXY_TARGET`.
  - Containers/Prod: configure reverse proxy at runtime using `API_BASE_URL`.
- Docker images:
  - Backend exposes 8080
  - Frontend exposes 80 (nginx)
- Keep README.md empty per request.
