# syntax=docker/dockerfile:1
# The web image: the SvelteKit SPA built to static files, served by Caddy, which also proxies /api to the API.
# Build context: the repository root (deploy/compose.yaml sets it).

# ---- build ----
FROM node:24-alpine AS build
WORKDIR /src/frontend
COPY frontend/package.json frontend/package-lock.json ./
# No browsers are needed here; tests run outside the image.
ENV PLAYWRIGHT_SKIP_BROWSER_DOWNLOAD=1
RUN npm ci --no-audit --no-fund
COPY frontend/ ./
RUN npm run build

# ---- runtime ----
FROM caddy:2-alpine AS runtime
COPY deploy/Caddyfile /etc/caddy/Caddyfile
COPY --from=build /src/frontend/build /srv
# :8080 by default (plain http); a domain name gets automatic HTTPS on 80/443 (see deploy/README.md).
ENV SITE_ADDRESS=:8080
EXPOSE 8080 80 443
