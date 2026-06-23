# syntax=docker/dockerfile:1

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy entire repo (use .dockerignore to keep it lean)
COPY . .

# CI-provided versioning (no MinVer inside the container)
ARG VERSION=0.0.0-dev
ARG INFORMATIONAL_VERSION=${VERSION}

# Restore and publish the Web project
RUN dotnet restore Vibes.ASBManager.sln
RUN dotnet publish src/Vibes.ASBManager.Web -c Release -o /app/publish -nologo \
    -p:Version=$VERSION \
    -p:InformationalVersion=$INFORMATIONAL_VERSION \
    -p:IncludeSourceRevisionInInformationalVersion=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Copy published output
COPY --from=build /app/publish .

# Data dir owned by the non-root 'app' user the base image provides
RUN mkdir -p /app/App_Data && chown app:app /app/App_Data && chmod 700 /app/App_Data
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Liveness probe: the app is accepting connections on its port
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
    CMD ["bash", "-c", "exec 3<>/dev/tcp/localhost/8080"]

# Drop root for the running container
USER app

# Run
ENTRYPOINT ["dotnet", "Vibes.ASBManager.Web.dll"]
