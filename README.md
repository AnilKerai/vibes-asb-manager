# Vibes ASB Manager

A Blazor Server tool for developers to explore and manage Azure Service Bus namespaces.

This repo supports two local development workflows:

- .NET Aspire (AppHost) — one command to run the app + Postgres
- Docker Compose — containerized Postgres + the Web app

The app persists connections in Postgres. A connection string named `asbdb` is required at runtime.

---

## Prerequisites

- .NET 9 SDK
- Docker Desktop (for Aspire or Docker Compose)

---

## Option A: Run with .NET Aspire (recommended)

This spins up Postgres and the Web app with service defaults (OpenTelemetry, health endpoints in dev, etc.).

```bash
# from repo root
DOTNET_CLI_TELEMETRY_OPTOUT=1 dotnet run -p src/Vibes.ASBManager.AppHost
```

- Open the Web URL printed in the console.
- Postgres data is stored in a Docker volume managed by Aspire and persists across restarts.

---

## Option B: Run with Docker Compose

This uses `docker-compose.yml` to run Postgres + the Web app. No .NET Aspire required.

```bash
# from repo root
docker compose up --build
# app will be available at http://localhost:8080
```

- The Web app uses the connection string `Host=db;Port=5432;Username=asb;Password=asb;Database=asbdb` provided via environment variables.
- Postgres data persists in the `asbpgdata` Docker volume.

Reset data (Compose):

```bash
docker compose down -v  # drops containers and the asbpgdata volume
```

---

## Option C: Run the Web app against your own Postgres

If you already have a Postgres instance running locally or in your infra, provide the `asbdb` connection string.

Using environment variables:

- macOS/Linux (bash/zsh):

```bash
export ConnectionStrings__asbdb="Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=asbdb"
dotnet run -p src/Vibes.ASBManager.Web
```

- Windows PowerShell:

```powershell
$env:ConnectionStrings__asbdb="Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=asbdb"
dotnet run -p src/Vibes.ASBManager.Web
```

Or use .NET User Secrets (from `src/Vibes.ASBManager.Web`):

```bash
cd src/Vibes.ASBManager.Web
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:asbdb" "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=asbdb"
dotnet run
```

---

## Project layout

- `src/Vibes.ASBManager.Web/` — Blazor Server app
- `src/Vibes.ASBManager.Application/` — app interfaces and contracts
- `src/Vibes.ASBManager.Infrastructure/` — Postgres store, Service Bus implementations
- `src/Vibes.ASBManager.Domain/` — domain models (e.g., `ConnectionInfo` with `Pinned`)
- `src/Vibes.ASBManager.AppHost/` — .NET Aspire orchestrator (dev-only)
- `src/Vibes.ASBManager.ServiceDefaults/` — shared service defaults (OpenTelemetry, health checks)

---

## Health endpoints (development)

When running in Development environment, the Web app maps:

- Readiness: `GET /health`
- Liveness: `GET /alive` (checks tagged as `live`)

These are mainly for local diagnostics and Compose/Aspire orchestration.

---

## Troubleshooting

- Missing connection string `asbdb`:
  - Provide the env var (see Option C), or run via Aspire/Compose.
- Port already in use:
  - Change the exposed port in `docker-compose.yml` or set `ASPNETCORE_URLS` when running locally.
- Docker Desktop issues:
  - Ensure it is running and has sufficient resources/disk space.

---

## Build locally (without running)

```bash
dotnet build Vibes.ASBManager.sln
```

---

## License

Internal developer tool. Add license here if you plan to distribute externally.
