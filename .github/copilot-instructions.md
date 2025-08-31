# Water Temperature Measurement System Architecture

## Project Structure & Conventions

### Monorepo Layout under `src/`
- **`backend/WaterTemperature.Api`** — ASP.NET 9 Web API with controller-based architecture
- **`frontend/app`** — React 18 + TypeScript + Vite app with Material-UI v6 ecosystem
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
- **React 18 + TypeScript + Vite** for development and building
- **Material-UI (MUI) v6** complete ecosystem:
  - `@mui/material` - Core components and theming system
  - `@mui/icons-material` - Icon library
  - `@mui/x-charts` - Charts and data visualization
  - `@mui/x-data-grid` - Advanced data tables and grids
  - `@mui/x-date-pickers` - Date/time picker components
  - `@mui/x-tree-view` - Hierarchical tree view components

### Template Foundation
- **MUI Dashboard Template**: Follow the structure from https://github.com/mui/material-ui/tree/v7.3.1/docs/data/material/getting-started/templates/dashboard
- **Component Architecture**:
  - `DashboardLayout` - Main layout wrapper with responsive sidebar navigation
  - `AppNavbar` - Top navigation bar with user menu and app controls
  - `SideMenu` - Collapsible sidebar navigation with routing
  - `MainGrid` - Content grid system for dashboard widgets and data display
  - `ProtectedRoute` - Route guards for authenticated content

### Theme & Styling System
- **Custom Theme**: Dark/light mode toggle with CSS variables enabled
- **Default Colors**: Dark mode with blue primary (#0ea5e9) and slate secondary colors
- **Responsive Design**: Mobile-first approach using MUI's breakpoint system
- **Material Design 3**: Following latest MUI design tokens and component variants

### State Management & Navigation
- **React Hooks**: useState, useEffect for local component state
- **React Router v6**: Client-side routing with programmatic navigation
- **Context API**: For global state like authentication and theme
- **API Integration**: Custom `api.ts` service layer with fetch-based HTTP client and JWT auth

### Authentication Flow
- **JWT-based authentication** with token storage and refresh
- **Route protection** via `ProtectedRoute` component wrapper
- **User registration/login** flows with form validation
- **Default test user** for development: `testuser` / `Test123!`

## Development Best Practices & Tooling

### Context7 Integration
- **Always use Context7** for best practices, code patterns, and following standard conventions
- **Architecture guidance** for .NET Core, React, TypeScript, and database design patterns
- **Code quality** enforcement through established industry standards

### MUI Development Guidelines
- **Always use the MUI MCP (Model Context Protocol)** when working with MUI components
- **Theme System**: Leverage MUI's CSS variables and theme customization
- **Responsive Design**: Use MUI's breakpoint system (`xs`, `sm`, `md`, `lg`, `xl`)
- **Component Composition**: Follow MUI's prop interfaces and composition patterns
- **Accessibility**: Implement ARIA support using MUI's built-in accessibility features
- **Performance**: Utilize MUI X components for advanced data presentation (charts, grids, etc.)

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
