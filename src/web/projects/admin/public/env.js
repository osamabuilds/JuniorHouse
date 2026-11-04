// Runtime config (not baked into the JS bundle), so the same build can point at a different API
// per environment. An empty apiBaseUrl means "this origin": `ng serve` forwards /api to the API
// through proxy.conf.json and the Docker image through nginx, so session cookies stay first-party.
// The Docker image overwrites this file at container start from the API_BASE_URL env var (see Dockerfile.admin).
window.__env = {
  apiBaseUrl: '',
};
