# Vibes Azure Service Bus Manager

A Blazor Server tool to explore and manage Azure Service Bus namespaces.

Connections are stored locally in `App_Data/connections.json` so you can run it anywhere without a database. Docker is the easiest way to get started.

## Disclaimer

I vibe-coded this tool. And yes, I hate myself for using that term! 😅

It is however, a useful technique to get a quick dev tool up and running though, so I don't apologize! 😂

With that in mind, feel free to suggest / make improvements - i'm not particularly precious about the code.

## Key features

- Browse queues, topics and subscriptions in a simple tree.
- Peek active and dead-letter messages with paging and optional live refresh.
- View message details and resubmit from DLQ; optionally remove the original from DLQ.
- Send to queues or topics with custom properties, content type, and scheduling; send multiple with an interval.
- Manage subscriptions and rules (SQL and correlation) with create/delete.
- Edit entity defaults (TTL and dead-letter on expiration) for queues, topics, subscriptions.
- Pinned connections and quick search for large lists.
- Data persisted to JSON; no external database required.

## Quick start (recommended): Docker CLI

Run the container and persist connection data in a named volume:

```bash
# optional: create the volume (auto-created on first run)
docker volume create vibes-asb-manager-data

docker run -d \
  --pull always \
  --name vibes-asb-manager \
  -p 9000:8080 \
  -v vibes-asb-manager-data:/app/App_Data \
  -e ASPNETCORE_ENVIRONMENT=Development \
  anilkerai/vibes-asb-manager-web:latest

# open http://localhost:9000
```

Pin to a specific version:

```bash
docker run -d \
  --name vibes-asb-manager \
  -p 9000:8080 \
  -v vibes-asb-manager-data:/app/App_Data \
  anilkerai/vibes-asb-manager-web:1.2.3
```

Upgrade later by pulling a new tag and recreating the container. Reset data by removing the named volume if needed.

## Alternative: Docker Compose (without cloning the repo)

Create a minimal `docker-compose.yml` locally and start the app:

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
      - "9000:8080"
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
     image: anilkerai/vibes-asb-manager-web:1.4.2
     pull_policy: always
   ```

Pinning a version ensures reproducible environments. Use `:latest` for always-up-to-date dev environments.

---

## Project layout

- `src/Vibes.ASBManager.Web/` — Blazor Server app
- `src/Vibes.ASBManager.Application/` — app interfaces and contracts
- `src/Vibes.ASBManager.Infrastructure.AzureServiceBus/` — Service Bus implementations
- `src/Vibes.ASBManager.Infrastructure.Storeage.File/` — File connection store implementations
- `src/Vibes.ASBManager.Domain/` — domain models (e.g., `ConnectionInfo` with `Pinned`)
- `src/Vibes.ASBManager.ServiceDefaults/` — shared service defaults (OpenTelemetry, health checks)
- `tests/Vibes.ASBManager.Tests.Unit/` — unit tests for valuable code

---

## Health endpoints (development)

When running in Development environment, the Web app maps:

- Readiness: `GET /health`
- Liveness: `GET /alive` (checks tagged as `live`)

These are mainly for local diagnostics and Compose orchestration.

---

## Troubleshooting

- App_Data not writable:
  - Ensure the running user has write permission to `App_Data`. In Docker, a named volume is mounted at `/app/App_Data`.
- Port already in use:
  - Change the exposed port in `docker-compose.yml` or set `ASPNETCORE_URLS` when running locally.
- Docker Desktop issues:
  - Ensure it is running and has sufficient resources/disk space.

---
