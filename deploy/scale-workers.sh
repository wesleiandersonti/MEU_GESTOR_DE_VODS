#!/usr/bin/env bash
set -Eeuo pipefail

APP_DIR="/opt/mgv-saas"
SCALE=${1:-${SCALE:-3}}

if [[ $EUID -ne 0 ]]; then
  echo "Execute como root (sudo)"
  exit 1
fi

if ! [[ "$SCALE" =~ ^[0-9]+$ ]] || [[ "$SCALE" -lt 1 ]]; then
  echo "Informe um número de instâncias válido (ex: 3)."
  exit 1
fi

cd "$APP_DIR"

if [[ -f "saas-gestor/docker-compose.yml" ]]; then
  cd saas-gestor
fi

echo "[MGV] Escalando workers para $SCALE instâncias..."
docker compose up -d --scale iptv-worker=$SCALE

docker compose ps
