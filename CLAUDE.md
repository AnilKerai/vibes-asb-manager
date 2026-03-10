# CLAUDE.md

## Project Overview

**Vibes Azure Service Bus Manager** is a Blazor Server web application for exploring and managing Azure Service Bus namespaces. It provides a UI for browsing and interacting with queues, topics, subscriptions, and messages. No external database — connections are stored locally in JSON.

## Architecture

Layered architecture with clean separation of concerns:

```
src/
├── Vibes.ASBManager.Web/                         # Blazor Server app (entry point)
├── Vibes.ASBManager.Application/                 # Interfaces & contracts
├── Vibes.ASBManager.Domain/                      # Domain models
├── Vibes.ASBManager.Infrastructure.AzureServiceBus/  # Azure SDK implementations
├── Vibes.ASBManager.Infrastructure.Storage.File/ # JSON file storage
└── Vibes.ASBManager.ServiceDefaults/             # Shared .NET Aspire defaults (OTel, health checks)

tests/
└── Vibes.ASBManager.Tests.Unit/                  # Unit tests (xunit)
```

**Tech stack:** C# / .NET 10, ASP.NET Core Blazor Server, MudBlazor 8.x, Azure.Messaging.ServiceBus 7.x, OpenTelemetry

## Build, Test & Run

```bash
# Build
dotnet build Vibes.ASBManager.sln --configuration Release

# Test
dotnet test Vibes.ASBManager.sln --configuration Release

# Run locally
cd src/Vibes.ASBManager.Web && dotnet run

# Run via Docker Compose
docker compose up -d
# Open http://localhost:9000
```

## Key Conventions

- **Nullable reference types** enabled across all projects
- **Implicit usings** enabled
- All I/O is **async/await** with `CancellationToken` support
- Infrastructure registered via extension methods: `.AddAzureServiceBusInfrastructure()`, `.AddFileStorage()`
- No controllers — Blazor components call application services directly
- Tests use **xunit**; no mocking framework enforced

## CI/CD

- **CI** (`.github/workflows/ci.yml`): builds and tests on .NET 9.0 for all branches/PRs
- **Docker** (`.github/workflows/docker.yml`): multi-arch (amd64 + arm64) images published to `anilkerai/vibes-asb-manager-web` on Docker Hub
- Version tags (`v*`) trigger semver-tagged releases; pushes to `main` produce short-SHA + `main` tags
