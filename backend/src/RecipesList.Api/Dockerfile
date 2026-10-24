# syntax=docker/dockerfile:1
# The API image (TECHNICAL §9). Build context: the repository root, e.g.
#   docker build -f backend/src/RecipesList.Api/Dockerfile -t recipes-list-api .
# deploy/compose.yaml builds it for you.

# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, from the project files and lock files only, so the layer is cached until a dependency changes.
COPY backend/global.json backend/Directory.Build.props backend/Directory.Packages.props backend/.editorconfig backend/
COPY backend/src/RecipesList.Core/RecipesList.Core.csproj backend/src/RecipesList.Core/packages.lock.json backend/src/RecipesList.Core/
COPY backend/src/RecipesList.Infrastructure/RecipesList.Infrastructure.csproj backend/src/RecipesList.Infrastructure/packages.lock.json backend/src/RecipesList.Infrastructure/
COPY backend/src/RecipesList.Api/RecipesList.Api.csproj backend/src/RecipesList.Api/packages.lock.json backend/src/RecipesList.Api/
RUN dotnet restore backend/src/RecipesList.Api/RecipesList.Api.csproj --locked-mode

COPY backend/src/ backend/src/
RUN dotnet publish backend/src/RecipesList.Api/RecipesList.Api.csproj -c Release --no-restore -o /app /p:UseAppHost=false

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    # Caddy is the only client; trust its X-Forwarded-For/-Proto (client IP for the auth rate limit, https scheme).
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true \
    Logging__Console__FormatterName=json \
    Database__MigrateOnStartup=true \
    Storage__ImagesPath=/data/images \
    Seed__Enabled=true \
    Seed__Path=/app/seed

COPY --from=build /app ./
COPY shared/seed/products.json shared/seed/tags.json shared/seed/utensils.json ./seed/

# Volumes are mounted on these; creating them for the non-root user gives a fresh named volume that owner.
RUN mkdir -p /data/images /home/app/.aspnet/DataProtection-Keys \
    && chown -R "$APP_UID" /data /home/app/.aspnet
USER $APP_UID
VOLUME ["/data/images", "/home/app/.aspnet/DataProtection-Keys"]
EXPOSE 8080

# The runtime image has no curl or wget: bash's /dev/tcp is enough for the liveness probe.
HEALTHCHECK --interval=10s --timeout=5s --start-period=30s --retries=5 \
    CMD ["bash", "-c", "exec 3<>/dev/tcp/127.0.0.1/8080 && printf 'GET /api/health HTTP/1.0\\r\\nHost: localhost\\r\\n\\r\\n' >&3 && head -n 1 <&3 | grep -q ' 200 '"]

ENTRYPOINT ["dotnet", "RecipesList.Api.dll"]
