import {
  AngularNodeAppEngine,
  createNodeRequestHandler,
  isMainModule,
  writeResponseToNodeResponse,
} from '@angular/ssr/node';
import express from 'express';
import { join } from 'node:path';

const browserDistFolder = join(import.meta.dirname, '../browser');

const app = express();
const angularApp = new AngularNodeAppEngine();

/**
 * Public storefront reads (settings, content pages) go to the API at `API_BASE_URL`. The browser and the server-side render both ask
 * this origin (`/api/storefront/...`), so the SSR server forwards them. Behind Cloudflare in production the same path can be routed
 * to the API directly. Only GET on `/api/storefront/` is forwarded: the admin routes are never reachable through the storefront.
 */
const apiBaseUrl = process.env['API_BASE_URL'] ?? 'http://localhost:5051';

app.get('/api/storefront/{*splat}', async (req, res) => {
  try {
    const headers: Record<string, string> = {};
    const ifNoneMatch = req.header('if-none-match');
    if (ifNoneMatch) headers['if-none-match'] = ifNoneMatch;
    const upstream = await fetch(new URL(req.originalUrl, apiBaseUrl), { headers, redirect: 'manual' });
    res.status(upstream.status);
    for (const name of ['content-type', 'etag', 'cache-control', 'location']) {
      const value = upstream.headers.get(name);
      if (value) res.setHeader(name, value);
    }
    res.send(Buffer.from(await upstream.arrayBuffer()));
  } catch {
    res.status(502).json({ title: 'The storefront could not reach the API.' });
  }
});

/**
 * Serve static files from /browser
 */
app.use(
  express.static(browserDistFolder, {
    maxAge: '1y',
    index: false,
    redirect: false,
  }),
);

/**
 * Handle all other requests by rendering the Angular application.
 */
app.use((req, res, next) => {
  angularApp
    .handle(req)
    .then((response) => (response ? writeResponseToNodeResponse(response, res) : next()))
    .catch(next);
});

/**
 * Start the server if this module is the main entry point, or it is ran via PM2.
 * The server listens on the port defined by the `PORT` environment variable, or defaults to 4000.
 */
if (isMainModule(import.meta.url) || process.env['pm_id']) {
  const port = process.env['PORT'] || 4000;
  app.listen(port, (error) => {
    if (error) {
      throw error;
    }

    console.log(`Node Express server listening on http://localhost:${port}`);
  });
}

/**
 * Request handler used by the Angular CLI (for dev-server and during build) or Firebase Cloud Functions.
 */
export const reqHandler = createNodeRequestHandler(app);
