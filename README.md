### Run with a minimal Compose file (without cloning the repo)

If you don't want to clone the source code, you can still run the app by creating a minimal `docker-compose.yml` locally:

```yaml
version: "3.9"
services:
  db:
    image: postgres:16
    environment:
      POSTGRES_USER: asb
      POSTGRES_PASSWORD: asb
      POSTGRES_DB: asbdb
    volumes:
      - asbpgdata:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U asb -d asbdb"]
      interval: 5s
      timeout: 3s
      retries: 20

  web:
    image: anilkerai/vibes-asb-manager-web:latest
    pull_policy: always
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ASPNETCORE_URLS: http://+:8080
      ConnectionStrings__asbdb: Host=db;Port=5432;Username=asb;Password=asb;Database=asbdb
    depends_on:
      db:
        condition: service_healthy
    ports:
      - "8080:8080"

volumes:
  asbpgdata:
```

Then run:

```bash
docker compose up -d
```

This starts Postgres and pulls the latest published image of the app — no source required.

---

## CI/CD: Build and publish Docker images

This repo includes a GitHub Actions workflow at `.github/workflows/docker.yml` that builds multi-arch images and publishes to Docker Hub.

### Configure repository secrets

Set the following GitHub repository secrets:

- `DOCKERHUB_USERNAME` — your Docker Hub username (e.g., `anilkerai`).
- `DOCKERHUB_TOKEN` — a Docker Hub Access Token for that account.

### What the workflow does

- Builds the Web app image using `src/Vibes.ASBManager.Web/Dockerfile`.
- Targets multi-arch: `linux/amd64, linux/arm64`.
- Publishes to `anilkerai/vibes-asb-manager-web` with tags:
  - On pushes to `main`: `main` and a short SHA tag (`sha-xxxxxxx`).
  - On tags like `v1.2.3`: `1.2.3`, `1.2`, and `latest`.
  - Manual runs via `workflow_dispatch` are supported.

### Releasing a new version

1. Merge to `main` or create a Git tag:
   ```bash
   git tag v1.2.3 && git push origin v1.2.3
   ```
2. The workflow will build and push multi-arch images.
3. Update your Compose file to pin to that version (recommended):
   ```yaml
   web:
     image: anilkerai/vibes-asb-manager-web:1.2.3
     pull_policy: always
   ```

Pinning a version ensures reproducible environments. Use `:latest` for always-up-to-date dev environments.
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

## Option B: Run with Docker Compose (uses published image)

This uses `docker-compose.yml` to run Postgres + the Web app. No .NET Aspire required. The `web` service pulls the published image `anilkerai/vibes-asb-manager-web:latest`.

```bash
# from repo root
docker compose up -d
# app will be available at http://localhost:8080
```

- The Web app uses the connection string `Host=db;Port=5432;Username=asb;Password=asb;Database=asbdb` provided via environment variables.
- Postgres data persists in the `asbpgdata` Docker volume.

Reset data (Compose):

```bash
docker compose down -v  # drops containers and the asbpgdata volume
```

### Pin to a specific image version with .env

You can control which image tag Compose pulls without editing YAML using a `.env` file (already git-ignored).

1) Create a `.env` file in the repo root with the tag you want:

```env
WEB_IMAGE=anilkerai/vibes-asb-manager-web:1.2.3
```

2) Start Compose as usual:

```bash
docker compose up -d
```

Compose will substitute `WEB_IMAGE` into `docker-compose.yml` (`image: ${WEB_IMAGE:-anilkerai/vibes-asb-manager-web:latest}`), so teams can pin versions consistently without changing the YAML.

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
