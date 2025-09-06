# Water Temperature Measurement System Architecture

## Project Structure & Conventions

### Monorepo Layout under `src/`
- **`backend/WaterTemperature.Api`** — ASP.NET 9 Web API with controller-based architecture
- **`frontend/app`** — React 18.3.1 + TypeScript + Vite app with Material-UI v7.2.0 ecosystem
- **`aspire/apphost`** — .NET Aspire 9.4.1 orchestrator for development/debugging
- **`aspire/servicedefaults`** — Shared Aspire services (OpenTelemetry, health checks, service discovery)
- **`esp32/`** — PlatformIO project placeholder for IoT sensors

### API Architecture
- **Controller-based API** (not minimal API) with `ApiControllerBase` providing common functionality
- **JWT Authentication** with BCrypt password hashing
- **Entity Framework Core 9** with PostgreSQL database
- **Structured Configuration** via strongly-typed settings classes
- **Health endpoints** at `/health` (development only for security)
- All API endpoints under `/api/*` prefix

### Database & Data Layer
- **PostgreSQL** as primary database
- **Entity Framework Core** with migrations
- **Database auto-creation** for development environments
- **Connection string management** via Aspire service discovery or Docker environment

### Deployment Flows
- **Development/Debugging**: Use Aspire composition (`dotnet run --project src/aspire/apphost`)
- **Production**: Use Docker Compose with multi-stage builds
- **Testing**: Copilot should only build projects and run tests, not start applications

### Network & Proxy Configuration
- **Development**: Vite dev server proxies `/api` to backend (port 8080)
- **Production**: Nginx reverse proxy handles `/api` routing to backend service
- **Docker**: Backend exposes 8080, Frontend exposes 80 (nginx)
- **Frontend reaches backend via**:
  - Dev: Vite proxy to `http://localhost:8080` or env `API_PROXY_TARGET`
  - Containers: Nginx proxy to backend service using `API_BASE_URL`

## Frontend Architecture & UI Guidelines

### Core Framework Stack
- **React 18.3.1 + TypeScript + Vite** for development and building
- **Material-UI (MUI) v7.2.0** complete ecosystem:
  - `@mui/material` - Core components with CSS variables theming
  - `@mui/icons-material` - Icon library
  - `@mui/x-charts` - Charts and data visualization
  - `@mui/x-data-grid` - Advanced data tables and grids
  - `@mui/x-date-pickers` - Date/time picker components
  - `@mui/x-tree-view` - Hierarchical tree view components

### Component Architecture
- **MainLayout** - Single layout component with responsive sidebar navigation
- **AppNavbar** - Mobile-only top navigation bar with menu toggle
- **SideMenu** - Desktop sidebar navigation (hidden on mobile)
- **SideMenuMobile** - Mobile drawer navigation
- **MainGrid** - Dashboard content grid with responsive temperature cards
- **ProtectedRoute** - Route guards for authenticated content

### Theme & Styling System
- **MUI v7 CSS Variables**: Class-based color scheme selector for performance
- **Default Colors**: Dark mode default with sky blue primary (#0ea5e9)
- **Mobile-First Design**: Responsive breakpoints with touch-friendly interactions
- **Shared Theme**: Consolidated theme system in `src/shared-theme/`

### State Management & Navigation
- **React Router v6.26.2**: Client-side routing with proper route protection
- **Auth Context**: JWT-based authentication with user profile management
- **API Integration**: Fetch-based HTTP client with JWT token handling

## Development Best Practices & Tooling

### Development & Debugging Workflow
- **Build and Test Only**: Copilot should only build projects and run tests - DO NOT start applications
- **Application Execution**: Two supported deployment flows only:
  1. **Development/Debugging**: `dotnet run --project src/aspire/apphost` (Aspire composition)
  2. **Production**: `docker compose up --build` (containerized deployment)

### Authentication & Testing
- **Default Test User**: Pre-seeded for development debugging:
  - Username: `testuser`
  - Password: `Test123!`
- **JWT Authentication**: Backend implements JWT tokens for API security
- **Database**: PostgreSQL with Entity Framework Core, auto-created on startup
- **Hot Reload**: Frontend supports Vite HMR for rapid development
