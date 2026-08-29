# ms-authz — MsAuthz.Api
# Build context MUST be the repo root (ms-authz/), not this directory, so the COPY paths below
# resolve. From the repo root: docker build -f src/MsAuthz.Api/Dockerfile .

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 6010

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files first so `dotnet restore` is cached across source-only changes.
COPY ["src/MsAuthz.Api/MsAuthz.Api.csproj", "src/MsAuthz.Api/"]
COPY ["src/MsAuthz.Application/MsAuthz.Application.csproj", "src/MsAuthz.Application/"]
COPY ["src/MsAuthz.Infrastructure/MsAuthz.Infrastructure.csproj", "src/MsAuthz.Infrastructure/"]

# No private NuGet feed needed here: OpenFga.Sdk is public. (Unlike estudio-contable-backend's
# Dockerfile, which needs a GitHub Packages secret for FileStore.Client — ms-authz has no such
# dependency in the server-side projects.)
RUN dotnet restore "src/MsAuthz.Api/MsAuthz.Api.csproj"

COPY src/MsAuthz.Api/ src/MsAuthz.Api/
COPY src/MsAuthz.Application/ src/MsAuthz.Application/
COPY src/MsAuthz.Infrastructure/ src/MsAuthz.Infrastructure/

WORKDIR "/src/src/MsAuthz.Api"
RUN dotnet build "MsAuthz.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "MsAuthz.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app

# curl for the HEALTHCHECK below — the slim aspnet image ships neither curl nor wget.
# (Runs as root here, before the USER switch.)
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=publish /app/publish .

RUN addgroup --system --gid 1001 msauthz \
    && adduser --system --uid 1001 --ingroup msauthz msauthz \
    && chown -R msauthz:msauthz /app
USER msauthz

HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:6010/health || exit 1

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:6010

ENTRYPOINT ["dotnet", "MsAuthz.Api.dll"]
