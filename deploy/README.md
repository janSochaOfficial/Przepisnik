# Deploying RecipesList

The full stack is three containers, defined in [compose.yaml](compose.yaml):

| Service | Image | What it does |
|---|---|---|
| `db` | `postgres:18` | The database, in the `pgdata` volume. Not published to the host. |
| `api` | built from `backend/src/RecipesList.Api/Dockerfile` (`mcr.microsoft.com/dotnet/aspnet:10.0`, non-root) | The API. Applies migrations and loads the catalog seed (`shared/seed`, baked into the image) at startup. Photos live in the `images` volume, the cookie keys in `dpkeys`. Not published: only Caddy reaches it. |
| `web` | built from `deploy/web.Dockerfile` (Node builds the SPA, `caddy:2-alpine` serves it) | Serves the static app, proxies `/api/*` to the API, compresses (zstd/gzip), adds security headers, and gets HTTPS certificates when `SITE_ADDRESS` is a domain. |

Health checks: `GET /api/health` (liveness, used by the image's `HEALTHCHECK`) and `GET /api/health/ready` (the database is reachable).

## First run (local, http://localhost:8080)

```bash
docker compose -f deploy/compose.yaml up -d --build
docker compose -f deploy/compose.yaml ps          # wait until api and web are "healthy"
```

Open http://localhost:8080. Without a Google client ID the login page says that sign-in is not configured yet; set one up as below.
Nothing has to be configured for a local try-out: every setting has a default. To change settings, copy the example and edit it:

```bash
cp deploy/.env.example deploy/.env   # compose reads deploy/.env automatically
```

The variables compose uses are in the "Docker Compose" part of [.env.example](.env.example): `HTTP_PORT`, `SITE_ADDRESS`, `POSTGRES_*`, `Auth__Google__ClientId`, `Admin__Emails__0`, `Seed__Enabled` and the `RateLimiting__*` limits. The API always runs as `Production` in this stack (dev-login is off).

Logs are JSON lines: `docker compose -f deploy/compose.yaml logs -f api`.

## Google sign-in

1. In the [Google Cloud console](https://console.cloud.google.com/apis/credentials), create (or pick) a project, configure the OAuth consent screen (type *External*, scopes `openid`, `email`, `profile`), then **Create credentials → OAuth client ID → Web application**.
2. Under **Authorized JavaScript origins** add every origin users open: `http://localhost:8080` for a local stack, `https://przepisy.example.com` for a real one. No redirect URIs are needed (the app uses Google Identity Services and sends the ID token to `POST /api/v1/auth/google`).
3. Put the client ID (`…apps.googleusercontent.com`) into `deploy/.env` as `Auth__Google__ClientId=…`, and the administrators' emails as `Admin__Emails__0=…` (`__1`, `__2` for more). Admin rights need a Google-verified email.
4. `docker compose -f deploy/compose.yaml up -d` to apply.

## HTTPS on a real domain

Point the domain's DNS at the server, then in `deploy/.env`:

```bash
SITE_ADDRESS=przepisy.example.com
```

Caddy then listens on 80 and 443 inside the container and obtains certificates automatically (they are kept in the `caddy_data` volume). Publish those ports with a small override file next to `compose.yaml`, for example `deploy/compose.override.yaml` (keep it out of git):

```yaml
services:
  web:
    ports:
      - "80:80"
      - "443:443"
```

and start with `docker compose -f deploy/compose.yaml -f deploy/compose.override.yaml up -d`. Change `POSTGRES_PASSWORD` before the first start on a server (the database is not published, but the default is public knowledge). The session cookie is `__Host-rl_session`, `Secure` and `SameSite=Lax`; it needs HTTPS everywhere except `localhost`.

## Backups

Two things hold data: the database and the photos volume.

```bash
# Database (consistent while the app runs)
docker compose -f deploy/compose.yaml exec -T db pg_dump -U recipes -d recipes -Fc > recipes-$(date +%F).dump

# Photos (the images volume, read through a throwaway container)
docker run --rm -v recipes-list_images:/data/images:ro -v "$PWD":/backup alpine \
  tar czf /backup/images-$(date +%F).tar.gz -C /data images
```

Restore into a fresh stack (`up -d` once so the volumes exist, then stop the API so nothing writes meanwhile):

```bash
docker compose -f deploy/compose.yaml stop api
docker compose -f deploy/compose.yaml exec -T db pg_restore -U recipes -d recipes --clean --if-exists < recipes-2026-10-24.dump
docker run --rm -v recipes-list_images:/data/images -v "$PWD":/backup alpine \
  sh -c 'rm -rf /data/images/* && tar xzf /backup/images-2026-10-24.tar.gz -C /data'
docker compose -f deploy/compose.yaml start api
```

The volume names carry the compose project name (`recipes-list` by default): check them with `docker volume ls`. Keep the `dpkeys` volume too if signing everyone out after a restore is a problem (it holds only the cookie signing keys).

## Upgrades

```bash
git pull
docker compose -f deploy/compose.yaml up -d --build
```

The API applies new database migrations when it starts and inserts catalog seed entries that are new (existing products are never overwritten). Take a database backup first: migrations are not rolled back automatically. Old images can be removed afterwards with `docker image prune`.

## End-to-end tests against this stack

`scripts/verify.sh --e2e` builds the stack with the [compose.e2e.yaml](compose.e2e.yaml) override (`ASPNETCORE_ENVIRONMENT=Testing`, so dev-login is on, and generous rate limits) under the separate project name `recipes-list-e2e`, runs `npm run test:e2e` in `frontend/` against it, and removes it again with its volumes. To run the suite by hand:

```bash
docker compose -p recipes-list-e2e -f deploy/compose.yaml -f deploy/compose.e2e.yaml up -d --build --wait
cd frontend && npm run test:e2e                     # E2E_BASE_URL defaults to http://localhost:8080
docker compose -p recipes-list-e2e -f deploy/compose.yaml -f deploy/compose.e2e.yaml down -v
```

Never use the E2E override for a real deployment.
