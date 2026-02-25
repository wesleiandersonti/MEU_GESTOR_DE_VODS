#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
ENV_FILE="${PROJECT_ROOT}/.env"

if [[ -f "${ENV_FILE}" ]]; then
  echo ".env ja existe — mantendo configuracao"
  exit 0
fi

umask 077

generate_secret() {
  if command -v openssl >/dev/null 2>&1; then
    openssl rand -hex 48
    return
  fi

  if command -v python3 >/dev/null 2>&1; then
    python3 - <<'PY'
import secrets
print(secrets.token_hex(48))
PY
    return
  fi

  head -c 48 /dev/urandom | od -An -tx1 | tr -d ' \n'
}

DB_ROOT_PASSWORD="$(generate_secret)"
DB_APP_PASSWORD="$(generate_secret)"
REDIS_PASSWORD="$(generate_secret)"
JWT_SECRET="$(generate_secret)"
JWT_REFRESH_SECRET="$(generate_secret)"
YOUTUBE_INTERNAL_API_KEY="$(generate_secret)"

cat > "${ENV_FILE}" <<EOF
DB_ROOT_PASSWORD=${DB_ROOT_PASSWORD}
DB_APP_PASSWORD=${DB_APP_PASSWORD}

REDIS_PASSWORD=${REDIS_PASSWORD}

JWT_SECRET=${JWT_SECRET}
JWT_REFRESH_SECRET=${JWT_REFRESH_SECRET}

YOUTUBE_INTERNAL_API_KEY=${YOUTUBE_INTERNAL_API_KEY}

NODE_ENV=production
EOF

chmod 600 "${ENV_FILE}"

echo ".env criado com sucesso"
