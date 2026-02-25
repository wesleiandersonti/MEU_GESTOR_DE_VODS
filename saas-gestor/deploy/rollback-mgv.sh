#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

cd "${PROJECT_ROOT}"

git rev-parse --is-inside-work-tree >/dev/null 2>&1
git fetch --tags --prune

echo "Tags disponiveis:"
git tag --sort=-v:refname

if [[ $# -ne 1 ]]; then
  echo "Uso: ./deploy/rollback-mgv.sh <tag>"
  exit 1
fi

TARGET_TAG="$1"

git rev-parse -q --verify "refs/tags/${TARGET_TAG}" >/dev/null

if [[ -n "$(git status --porcelain)" ]]; then
  echo "Repositorio com alteracoes locais. Limpe antes do rollback."
  exit 1
fi

docker compose down

git checkout "${TARGET_TAG}"

bash "${SCRIPT_DIR}/bootstrap-env.sh"

docker compose up -d --build backend youtube-live-manager

docker compose ps

curl -fsS "http://localhost:3000/api/v1/health" >/dev/null

docker compose exec -T backend node -e "fetch('http://youtube-live-manager:8787/health').then(r=>r.ok?process.exit(0):process.exit(1)).catch(()=>process.exit(1))"
