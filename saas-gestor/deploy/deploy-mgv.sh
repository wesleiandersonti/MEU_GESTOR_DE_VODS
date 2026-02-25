#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 0 ]]; then
  echo "Uso: ./deploy/deploy-mgv.sh"
  exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

cd "${PROJECT_ROOT}"

git rev-parse --is-inside-work-tree >/dev/null 2>&1

git pull --ff-only

bash "${SCRIPT_DIR}/bootstrap-env.sh"

docker compose up -d --build backend youtube-live-manager

docker compose ps

curl -fsS "http://localhost:3000/api/v1/health" >/dev/null

docker compose exec -T backend node -e "fetch('http://youtube-live-manager:8787/health').then(r=>r.ok?process.exit(0):process.exit(1)).catch(()=>process.exit(1))"
