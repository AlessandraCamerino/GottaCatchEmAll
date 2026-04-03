# Multi-stage build
#
# Design decision: we use a multi-stage Dockerfile to keep the final image small.
#   - Stage 1 (build): uses the full SDK image to restore, build and publish
#   - Stage 2 (runtime): uses the lightweight ASP.NET runtime image (no SDK tools)
#
# This is the standard production pattern — the SDK image is ~800MB, the runtime ~200MB.

# -------------------------------------------------------------------
# Stage 1: build
# -------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files first to leverage Docker layer caching.
# If only source files change (not .csproj), the restore layer is not invalidated.
COPY Pokedex.sln ./
COPY src/Pokedex.Api/Pokedex.Api.csproj src/Pokedex.Api/

RUN dotnet restore src/Pokedex.Api/Pokedex.Api.csproj

# Copy the rest of the source and publish
COPY src/Pokedex.Api/ src/Pokedex.Api/

RUN dotnet publish src/Pokedex.Api/Pokedex.Api.csproj \
    --configuration Release \
    --output /app/publish

# -------------------------------------------------------------------
# Stage 2: runtime
# -------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Run as a non-root user for security.
# Design decision: avoid running the process as root inside the container —
# if the application is compromised, the attacker has limited privileges.
RUN adduser --disabled-password --gecos "" appuser
USER appuser

COPY --from=build /app/publish .

# ASP.NET Core listens on port 8080 by default when running in a container
# (ASPNETCORE_HTTP_PORTS defaults to 8080 in the aspnet base image).
EXPOSE 8080

ENTRYPOINT ["dotnet", "Pokedex.Api.dll"]
