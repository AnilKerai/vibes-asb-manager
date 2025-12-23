# syntax=docker/dockerfile:1

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy entire repo (use .dockerignore to keep it lean)
COPY . .

# Restore and publish the Web project
RUN apt-get update && apt-get install -y git && rm -rf /var/lib/apt/lists/*
RUN dotnet restore Vibes.ASBManager.sln
RUN dotnet publish src/Vibes.ASBManager.Web -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Copy published output
COPY --from=build /app/publish .

# Configure ASP.NET Core
RUN mkdir -p /app/App_Data && \
    chown -R 0:0 /app && chmod -R 777 /app/App_Data
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Run
ENTRYPOINT ["dotnet", "Vibes.ASBManager.Web.dll"]
