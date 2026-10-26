#!/bin/sh
# Regenerates env.js from the API_BASE_URL env var before nginx starts, so the same built image
# can point at a different API per environment without a rebuild. Empty (the default) means the
# admin calls its own origin and nginx forwards /api to API_UPSTREAM (sessions G9).
set -eu

cat > /usr/share/nginx/html/env.js <<EOF2
window.__env = {
  apiBaseUrl: "${API_BASE_URL:-}",
};
EOF2
