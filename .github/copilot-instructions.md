Coding conventions and project map

- Monorepo layout under `src/`
  - `backend/WaterTemperature.Api` — ASP.NET 8 minimal API
  - `frontend/app` — React + Vite app with Material-UI
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

## Frontend UI Guidelines

- **Framework**: React 18 + TypeScript + Vite
- **UI Library**: Material-UI (MUI) v6 with complete ecosystem:
  - `@mui/material` - Core components
  - `@mui/icons-material` - Icons
  - `@mui/x-charts` - Charts and data visualization
  - `@mui/x-data-grid` - Advanced data tables
  - `@mui/x-date-pickers` - Date/time pickers
  - `@mui/x-tree-view` - Tree view components
- **Template Base**: Follow the MUI Dashboard template structure from: https://github.com/mui/material-ui/tree/v7.3.1/docs/data/material/getting-started/templates/dashboard
- **Theme**: 
  - Custom dark/light mode theme with CSS variables enabled
  - Default: Dark mode with blue primary (#0ea5e9) and slate secondary colors
  - Responsive design with mobile-first approach
- **Layout Structure**:
  - `DashboardLayout` - Main layout wrapper with sidebar navigation
  - `AppNavbar` - Top navigation bar with user menu
  - `SideMenu` - Collapsible sidebar navigation
  - `MainGrid` - Content grid system for dashboard widgets
- **Navigation**: React Router v6 with programmatic navigation
- **State Management**: React hooks (useState, useEffect) for local state
- **API Integration**: Custom `api.ts` service layer with fetch-based HTTP client

## MUI Development Guidelines

- **Always use the MUI MCP (Model Context Protocol)** when working with MUI components
- Leverage MUI's theme system and CSS variables for consistent styling
- Use MUI's responsive breakpoint system for mobile-first design
- Follow MUI's component composition patterns and prop interfaces
- Utilize MUI X components for advanced data presentation (charts, grids, etc.)
- Implement proper accessibility features using MUI's built-in ARIA support

## Development & Debugging

- **Default Test User**: For easier debugging, the Aspire project seeds a default user:
  - Username: `testuser`
  - Password: `Test123!`
  - This eliminates the need to register/create users during development
- **JWT Authentication**: Backend uses JWT tokens for API authentication
- **Database**: PostgreSQL with Entity Framework Core, auto-created on startup
- **Hot Reload**: Frontend supports hot module replacement via Vite dev server
