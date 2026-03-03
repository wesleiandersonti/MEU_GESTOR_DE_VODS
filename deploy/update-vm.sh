#!/usr/bin/env bash
set -Eeuo pipefail

APP_DIR="/opt/mgv-saas"

if [[ $EUID -ne 0 ]]; then
  echo "Execute como root (sudo)"
  exit 1
fi

if [[ ! -d "$APP_DIR/.git" ]]; then
  echo "[MGV] Repositório não encontrado em $APP_DIR. Rode o install-vm primeiro."
  exit 1
fi

cd "$APP_DIR"

echo "[MGV] Atualizando código..."
git pull --ff-only

if [[ -f "saas-gestor/docker-compose.yml" ]]; then
  cd saas-gestor
fi

if [[ -f "bootstrap-env.sh" ]]; then
  chmod +x bootstrap-env.sh
  ./bootstrap-env.sh
fi

echo "[MGV] Rebuild containers..."
docker compose up -d --build --remove-orphans

echo "[MGV] Atualizado com sucesso."
docker compose ps
