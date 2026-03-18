#!/usr/bin/env bash
set -euo pipefail

if [[ "${EUID}" -ne 0 ]]; then
  echo "Run this script with sudo." >&2
  exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SERVICE_USER="${SERVICE_USER:-rhino-compute}"
COMPUTE_ENV_FILE="${COMPUTE_ENV_FILE:-/etc/rhino-compute/environment}"
RESTART_SERVICE="${RESTART_SERVICE:-false}"

YAK_PACKAGE="$(find "$SCRIPT_DIR/packages" -maxdepth 1 -type f -name '*.yak' | sort | head -n 1)"
if [[ -z "${YAK_PACKAGE}" ]]; then
  echo "No local .yak package found in $SCRIPT_DIR/packages" >&2
  exit 1
fi

if ! command -v yak >/dev/null 2>&1; then
  echo "yak CLI was not found on PATH." >&2
  exit 1
fi

SERVICE_HOME="$(getent passwd "$SERVICE_USER" | cut -d: -f6)"
if [[ -z "${SERVICE_HOME}" ]]; then
  echo "Could not resolve home directory for service user '$SERVICE_USER'." >&2
  exit 1
fi

mkdir -p "$SERVICE_HOME"
chown "$SERVICE_USER:$SERVICE_USER" "$SERVICE_HOME"

su -s /bin/bash "$SERVICE_USER" -c "HOME='$SERVICE_HOME' yak uninstall assertive-possum >/dev/null 2>&1 || true"
su -s /bin/bash "$SERVICE_USER" -c "HOME='$SERVICE_HOME' yak install '$YAK_PACKAGE'"

if [[ -f "$COMPUTE_ENV_FILE" ]]; then
  if grep -Eq '^[#[:space:]]*RHINO_COMPUTE_LOAD_GRASSHOPPER=' "$COMPUTE_ENV_FILE"; then
    sed -i -E 's|^[#[:space:]]*RHINO_COMPUTE_LOAD_GRASSHOPPER=.*|RHINO_COMPUTE_LOAD_GRASSHOPPER=true|' "$COMPUTE_ENV_FILE"
  else
    printf '\nRHINO_COMPUTE_LOAD_GRASSHOPPER=true\n' >> "$COMPUTE_ENV_FILE"
  fi
fi

if [[ "$RESTART_SERVICE" == "true" ]] && command -v systemctl >/dev/null 2>&1; then
  systemctl restart rhino-compute
  echo "Restarted rhino-compute."
fi

echo "Assertive Possum was installed from $YAK_PACKAGE for user $SERVICE_USER."
