#!/usr/bin/env bash
# The quality gate (docs/IMPLEMENTATION_PLAN.md §4.6). The only definition of "green".
#
#   scripts/verify.sh            full mode (coordinator, CI): every step, all backend tests
#   scripts/verify.sh --agent    agent mode: skips the MigrationsAreInSync test (Category=Migrations)
#   scripts/verify.sh --e2e      full mode plus E2E against the compose stack (added by T90)
#
# Stops at the first failing step and names it. Works from any directory and any worktree.
set -euo pipefail

usage() {
	sed -n '2,8p' "$0" | sed 's/^# \{0,1\}//'
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

on_exit() {
	local status=$?
	if [[ $status -ne 0 && -n "$CURRENT_STEP" ]]; then
		printf '\n\033[1;31mFAILED at step: %s\033[0m (exit %d)\n' "$CURRENT_STEP" "$status" >&2
	fi
}
trap on_exit EXIT

step() {
	CURRENT_STEP="$1"
	printf '\n\033[1;34m==> %s\033[0m\n' "$CURRENT_STEP"
}

if [[ "$MODE" == "e2e" ]]; then
	step "E2E prerequisites"
	echo "The E2E suite and the compose stack are added by T90; --e2e is not available yet." >&2
	exit 1
fi

echo "verify.sh: $MODE mode in $ROOT"

step "Tooling present (dotnet, node, npm, git)"
for tool in dotnet node npm git; do
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

step "Frontend: npm run check"
(cd "$FRONTEND" && npm run --silent check)

step "Frontend: npm run lint"
(cd "$FRONTEND" && npm run --silent lint)

step "Frontend: npm test"
(cd "$FRONTEND" && npm run --silent test)

step "Frontend: npm run test:integration (Playwright, mocked API)"
(cd "$FRONTEND" && npm run --silent test:integration)

CURRENT_STEP=""
printf '\n\033[1;32mverify.sh: all steps passed (%s mode, %ds)\033[0m\n' "$MODE" "$((SECONDS - START_TIME))"
