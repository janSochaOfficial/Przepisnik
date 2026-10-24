#!/usr/bin/env bash
# The quality gate (docs/IMPLEMENTATION_PLAN.md §4.6). The only definition of "green".
#
#   scripts/verify.sh            full mode (coordinator, CI): every step, all backend tests
#   scripts/verify.sh --agent    agent mode: skips the MigrationsAreInSync test (Category=Migrations)
#   scripts/verify.sh --e2e      full mode plus the E2E smoke suite against the compose stack
#                                (deploy/compose.yaml + compose.e2e.yaml as project recipes-list-e2e, always
#                                torn down with its volumes; E2E_HTTP_PORT picks the host port, default 8080)
#
# Stops at the first failing step and names it. Works from any directory and any worktree.
set -euo pipefail

usage() {
	sed -n '2,10p' "$0" | sed 's/^# \{0,1\}//'
}

MODE=full
for arg in "$@"; do
	case "$arg" in
		--agent) MODE=agent ;;
		--e2e) MODE=e2e ;;
		-h | --help)
			usage
			exit 0
			;;
		*)
			echo "verify.sh: unknown option '$arg'" >&2
			usage >&2
			exit 2
			;;
	esac
done

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BACKEND="$ROOT/backend"
FRONTEND="$ROOT/frontend"

CURRENT_STEP=""
START_TIME=$SECONDS
CLEANUP=""

on_exit() {
	local status=$?
	if [[ -n "$CLEANUP" ]]; then
		eval "$CLEANUP" || true
	fi
	if [[ $status -ne 0 && -n "$CURRENT_STEP" ]]; then
		printf '\n\033[1;31mFAILED at step: %s\033[0m (exit %d)\n' "$CURRENT_STEP" "$status" >&2
	fi
}
trap on_exit EXIT

step() {
	CURRENT_STEP="$1"
	printf '\n\033[1;34m==> %s\033[0m\n' "$CURRENT_STEP"
}

echo "verify.sh: $MODE mode in $ROOT"

step "Tooling present (dotnet, node, npm, git)"
TOOLS=(dotnet node npm git)
if [[ "$MODE" == "e2e" ]]; then TOOLS+=(docker); fi
for tool in "${TOOLS[@]}"; do
	command -v "$tool" >/dev/null || {
		echo "missing tool: $tool" >&2
		exit 1
	}
done

step "Frontend dependencies (npm ci when node_modules is missing or stale)"
# npm writes node_modules/.package-lock.json on every install; if the lockfile is newer, reinstall.
if [[ ! -f "$FRONTEND/node_modules/.package-lock.json" ||
	"$FRONTEND/package-lock.json" -nt "$FRONTEND/node_modules/.package-lock.json" ]]; then
	(cd "$FRONTEND" && npm ci)
else
	echo "node_modules is up to date"
fi

step "Backend: dotnet format --verify-no-changes"
(cd "$BACKEND" && dotnet format RecipesList.slnx --verify-no-changes)

step "Backend: dotnet build (warnings are errors)"
(cd "$BACKEND" && dotnet build RecipesList.slnx --nologo -warnaserror)

if [[ "$MODE" == "agent" ]]; then
	step "Backend: dotnet test (unit + integration, excluding Category=Migrations)"
	(cd "$BACKEND" && dotnet test RecipesList.slnx --no-build --nologo --filter "Category!=Migrations")
else
	step "Backend: dotnet test (unit + integration, all)"
	(cd "$BACKEND" && dotnet test RecipesList.slnx --no-build --nologo)
fi

step "Contract: redocly lint docs/api/openapi.yaml"
(cd "$FRONTEND" && npm run --silent lint:api)

step "Contract: npm run gen:api, schema.d.ts must be unchanged"
(cd "$FRONTEND" && npm run --silent gen:api)
git -C "$ROOT" diff --exit-code -- frontend/src/lib/api/schema.d.ts || {
	echo "schema.d.ts is out of date with docs/api/openapi.yaml: commit the regenerated file." >&2
	exit 1
}

step "Shared: seed and test-vector JSON validation"
(cd "$FRONTEND" && npm run --silent validate:shared)

step "Shared: seed rules (node shared/seed/validate.mjs)"
node "$ROOT/shared/seed/validate.mjs"

step "Frontend: npm run check"
(cd "$FRONTEND" && npm run --silent check)

step "Frontend: npm run lint"
(cd "$FRONTEND" && npm run --silent lint)

step "Frontend: npm test"
(cd "$FRONTEND" && npm run --silent test)

step "Frontend: npm run test:integration (Playwright, mocked API)"
(cd "$FRONTEND" && npm run --silent test:integration)

if [[ "$MODE" == "e2e" ]]; then
	E2E_HTTP_PORT="${E2E_HTTP_PORT:-8080}"
	compose_e2e() {
		HTTP_PORT="$E2E_HTTP_PORT" docker compose -p recipes-list-e2e \
			-f "$ROOT/deploy/compose.yaml" -f "$ROOT/deploy/compose.e2e.yaml" "$@"
	}

	step "E2E: host port $E2E_HTTP_PORT is free"
	if (exec 3<>"/dev/tcp/127.0.0.1/$E2E_HTTP_PORT") 2>/dev/null; then
		echo "Something already listens on port $E2E_HTTP_PORT: stop it or set E2E_HTTP_PORT." >&2
		exit 1
	fi

	step "E2E: docker compose up --build --wait (db, api, web)"
	# From here on the stack is always removed again, volumes included, whatever happens.
	CLEANUP='echo "verify.sh: removing the E2E stack"; compose_e2e down -v --remove-orphans >/dev/null 2>&1'
	compose_e2e up -d --build --wait

	step "E2E: npm run test:e2e (Playwright against http://localhost:$E2E_HTTP_PORT)"
	(cd "$FRONTEND" && E2E_BASE_URL="http://localhost:$E2E_HTTP_PORT" npm run --silent test:e2e)
fi

CURRENT_STEP=""
printf '\n\033[1;32mverify.sh: all steps passed (%s mode, %ds)\033[0m\n' "$MODE" "$((SECONDS - START_TIME))"
