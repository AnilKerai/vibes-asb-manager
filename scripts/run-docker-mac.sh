#!/usr/bin/env bash
set -euo pipefail

# Simple runner for macOS/Linux to start the app container.
# You can override defaults via environment variables:
#   WEB_IMAGE (default: anilkerai/vibes-asb-manager-web:latest)
#   VOLUME_NAME (default: vibes-asb-manager-data)
#   CONTAINER_NAME (default: vibes-asb-manager)
#   PORT (default: 9000)

IMAGE="${WEB_IMAGE:-anilkerai/vibes-asb-manager-web:latest}"
VOLUME_NAME="${VOLUME_NAME:-vibes-asb-manager-data}"
CONTAINER_NAME="${CONTAINER_NAME:-vibes-asb-manager}"
PORT="${PORT:-9000}"

if ! command -v docker >/dev/null 2>&1; then
  echo "Docker is not installed or not in PATH. Please install Docker Desktop." >&2
  exit 1
fi

# Create the named volume if it doesn't exist
if ! docker volume inspect "$VOLUME_NAME" >/dev/null 2>&1; then
  docker volume create "$VOLUME_NAME" >/dev/null
fi

# Remove any existing container of the same name (ignore errors)
docker rm -f "$CONTAINER_NAME" >/dev/null 2>&1 || true

# Run the container (always pull to avoid stale cache)
docker run --pull always -d \
  --name "$CONTAINER_NAME" \
  -p "$PORT:8080" \
  -v "$VOLUME_NAME:/app/App_Data" \
  -e ASPNETCORE_ENVIRONMENT=Development \
  "$IMAGE"

URL="http://localhost:${PORT}"
echo "Container '$CONTAINER_NAME' is running. Open your browser at: $URL"
