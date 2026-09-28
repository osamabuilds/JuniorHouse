#!/bin/sh
# Regenerates env.js from the API_BASE_URL env var before nginx starts, so the same built image
# can point at a different API per environment without a rebuild.
set -eu

cat > /usr/share/nginx/html/env.js <<EOF
window.__env = {
  apiBaseUrl: "${API_BASE_URL}",
};
EOF
