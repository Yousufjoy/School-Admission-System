#!/bin/sh
set -eu

ESC_BACKEND=$(printf '%s' "${VITE_BACKEND_URL:-}" | sed 's/\\/\\\\/g; s/"/\\"/g')
ESC_API=$(printf '%s' "${VITE_API_BASE_URL:-}" | sed 's/\\/\\\\/g; s/"/\\"/g')

cat <<EOF > /usr/share/nginx/html/env-config.js
window.__APP_CONFIG__ = {
  VITE_BACKEND_URL: "${ESC_BACKEND}",
  VITE_API_BASE_URL: "${ESC_API}"
};
EOF

