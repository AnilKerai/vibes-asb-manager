### Run with a minimal Compose file (without cloning the repo)

If you don't want to clone the source code, you can still run the app by creating a minimal `docker-compose.yml` locally:

```yaml
version: "3.9"
services:
  web:
    image: anilkerai/vibes-asb-manager-web:latest
    pull_policy: always
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ASPNETCORE_URLS: http://+:8080
    ports:
      - "8080:8080"
    volumes:
      - appdata:/app/App_Data

volumes:
  appdata:
```

Then run:

```bash
docker compose up -d
```

This starts the Web app and persists `App_Data` via a volume — no source required.

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

Storage: The app persists connections in a JSON file at `App_Data/connections.json`. No database is required.

Local dev options:

- .NET Aspire (AppHost) — orchestrates the Web app only (no DB)
- Docker Compose — pulls the published image and persists `App_Data` via a volume

---

## Prerequisites

- .NET 9 SDK
- Docker Desktop (for Aspire or Docker Compose)

---

## Option A: Run with .NET Aspire (recommended)

This starts the Web app with service defaults (OpenTelemetry, health endpoints in dev, etc.). No database is used.

```bash
# from repo root
DOTNET_CLI_TELEMETRY_OPTOUT=1 dotnet run -p src/Vibes.ASBManager.AppHost
```

- Open the Web URL printed in the console.
- Connection data is stored in `App_Data/connections.json`.

---

## Option B: Run with Docker Compose (uses published image)

This uses `docker-compose.yml` to run the Web app (no DB). The `web` service pulls the published image `anilkerai/vibes-asb-manager-web:latest` and mounts a volume at `/app/App_Data` for persistence.

```bash
# from repo root
docker compose up -d
# app will be available at http://localhost:8080
```

- Connection data persists in the `appdata` Docker volume (mounted to `/app/App_Data`).

Reset data (Compose):

```bash
docker compose down -v  # drops containers and the appdata volume
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

## Option C: Run locally (no Docker)

From the repo root:

```bash
dotnet run -p src/Vibes.ASBManager.Web
```

The app will store data in `src/Vibes.ASBManager.Web/App_Data/connections.json`.

---

## Project layout

- `src/Vibes.ASBManager.Web/` — Blazor Server app
- `src/Vibes.ASBManager.Application/` — app interfaces and contracts
- `src/Vibes.ASBManager.Infrastructure/` — JSON connection store and Service Bus implementations
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

- App_Data not writable:
  - Ensure the running user has write permission to `App_Data`. In Docker, a named volume is mounted at `/app/App_Data`.
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
