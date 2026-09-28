// Runtime config (not baked into the JS bundle), so the same build can point at a different API
// per environment. Local `ng serve`/`npm start` uses this default; the Docker image overwrites
// this file at container start from the API_BASE_URL env var (see Dockerfile.admin).
window.__env = {
  apiBaseUrl: 'http://localhost:5051',
};
