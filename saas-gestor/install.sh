#!/usr/bin/env bash
set -Eeuo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [[ -x "${SCRIPT_DIR}/../deploy/install-vm.sh" ]]; then
  exec "${SCRIPT_DIR}/../deploy/install-vm.sh" "$@"
fi

exec "${SCRIPT_DIR}/deploy/install-vm.sh" "$@"
