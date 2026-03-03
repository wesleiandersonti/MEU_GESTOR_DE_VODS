#!/usr/bin/env bash
set -Eeuo pipefail

APP_DIR="/opt/mgv-saas"
REPO_URL="https://github.com/wesleiandersonti/MEU_GESTOR_DE_VODS.git"
BRANCH="main"

if [[ $EUID -ne 0 ]]; then
  echo "Execute como root (sudo)"
  exit 1
fi

export DEBIAN_FRONTEND=noninteractive

echo "[MGV] Atualizando pacotes..."
apt update -y
apt install -y git curl ca-certificates gnupg lsb-release python3

if ! command -v docker >/dev/null 2>&1; then
  echo "[MGV] Instalando Docker..."
  curl -fsSL https://get.docker.com | sh
fi

echo "[MGV] Garantindo Docker ativo no boot..."
systemctl enable docker
systemctl start docker

if ! docker compose version >/dev/null 2>&1; then
  echo "[MGV] Instalando plugin docker compose..."
  apt install -y docker-compose-plugin || true
fi

if ! docker compose version >/dev/null 2>&1; then
  echo "[MGV] Instalando compose plugin manualmente..."
  mkdir -p /usr/local/lib/docker/cli-plugins
  curl -fsSL "https://github.com/docker/compose/releases/latest/download/docker-compose-linux-$(uname -m)" -o /usr/local/lib/docker/cli-plugins/docker-compose
  chmod +x /usr/local/lib/docker/cli-plugins/docker-compose
fi

mkdir -p "$APP_DIR"

if [[ -d "$APP_DIR/.git" ]]; then
  echo "[MGV] Repositório já existe. Atualizando..."
  git -C "$APP_DIR" fetch --all --prune
  git -C "$APP_DIR" checkout "$BRANCH"
  git -C "$APP_DIR" pull --ff-only origin "$BRANCH"
else
  if [[ -n "$(ls -A "$APP_DIR" 2>/dev/null)" ]]; then
    echo "[MGV] Diretório $APP_DIR não está vazio e não é git. Limpe-o antes de continuar."
    exit 1
  fi

  echo "[MGV] Clonando repositório..."
  git clone --branch "$BRANCH" "$REPO_URL" "$APP_DIR"
fi

cd "$APP_DIR"

if [[ -f "saas-gestor/docker-compose.yml" ]]; then
  cd saas-gestor
fi

chmod +x deploy/*.sh || true

if [[ -f "bootstrap-env.sh" ]]; then
  chmod +x bootstrap-env.sh
  ./bootstrap-env.sh
fi

echo "[MGV] Subindo containers..."
docker compose up -d --build --remove-orphans

echo "[MGV] Status final:"
docker compose ps

IP=$(hostname -I | awk '{print $1}')
if [[ -z "$IP" ]]; then
  IP="localhost"
fi

echo ""
echo "Frontend: http://$IP"
echo "Backend:  http://$IP:3000"
