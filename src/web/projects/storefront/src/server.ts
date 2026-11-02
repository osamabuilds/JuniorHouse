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
 * The signed-in customer's address book (ADD-*). The browser asks this origin (`/customer-api/addresses`) and the server forwards to
 * `/api/customers/me/addresses` with the customer's cookie. Nothing else under `/api/customers` is reachable this way, and the answer
 * is never cached.
 */
const addressBook = /^\/customer-api\/addresses(\/[0-9]+(\/default)?)?$/;

app.all('/customer-api/{*splat}', express.json({ limit: '16kb' }), async (req, res, next) => {
  if (req.path === '/customer-api/config' || req.path.startsWith('/customer-api/auth/') || req.path === '/customer-api/cart' || req.path.startsWith('/customer-api/cart/') || req.path === '/customer-api/orders' || req.path.startsWith('/customer-api/orders/')) {
    next(); // the sign-in routes below
    return;
  }
  if (!addressBook.test(req.path) || !['GET', 'POST', 'PUT', 'DELETE'].includes(req.method)) {
    res.status(404).end();
    return;
  }
  try {
    const headers: Record<string, string> = {};
    const cookie = req.header('cookie');
    if (cookie) headers['cookie'] = cookie;
    const sendsJson = (req.method === 'POST' || req.method === 'PUT') && req.body && Object.keys(req.body).length > 0;
    if (sendsJson) headers['content-type'] = 'application/json';
    const target = new URL(req.path.replace('/customer-api/addresses', '/api/customers/me/addresses'), apiBaseUrl);
    target.search = new URL(req.originalUrl, apiBaseUrl).search;
    const upstream = await fetch(target, {
      method: req.method,
      headers,
      body: sendsJson ? JSON.stringify(req.body) : undefined,
      redirect: 'manual',
    });
    res.status(upstream.status);
    const type = upstream.headers.get('content-type');
    if (type) res.setHeader('content-type', type);
    res.setHeader('cache-control', 'private, no-store');
    res.send(Buffer.from(await upstream.arrayBuffer()));
  } catch {
    res.status(502).json({ title: 'The storefront could not reach the API.' });
  }
});

/**
 * Customer sign-in (accounts ACT-1 to ACT-14, ACT-27, ACT-28). The browser asks this origin (`/customer-api/auth/...`); the server
 * forwards to `/api/customers/auth/...` and carries the cookies both ways, so the session cookies belong to the storefront origin
 * (Q-ACT-5, ADR 0023). Only the routes in the list below are reachable, with the method each accepts. The browser's `Origin` and the
 * anti-forgery header go through unchanged (the API checks both); the real client address goes in `x-forwarded-for`. Answers are never cached.
 */
const authRoutes: Readonly<Record<string, readonly string[]>> = {
  '/password-rules': ['GET'],
  '/session': ['GET'],
  '/register': ['POST'],
  '/sign-in': ['POST'],
  '/refresh': ['POST'],
  '/sign-out': ['POST'],
  '/email/verify': ['POST'],
  '/email/resend': ['POST'],
  '/password-reset/request': ['POST'],
  '/password-reset/confirm': ['POST'],
};

app.all('/customer-api/auth/{*splat}', express.json({ limit: '4kb' }), async (req, res) => {
  const route = req.path.replace('/customer-api/auth', '');
  if (!authRoutes[route]?.includes(req.method)) {
    res.status(404).end();
    return;
  }
  try {
    const headers: Record<string, string> = {};
    for (const name of ['cookie', 'origin', 'user-agent', 'x-romp-csrf']) {
      const value = req.header(name);
      if (value) headers[name] = value;
    }
    const forwardedFor = [req.header('x-forwarded-for'), req.socket.remoteAddress].filter(Boolean).join(', ');
    if (forwardedFor) headers['x-forwarded-for'] = forwardedFor;
    const sendsJson = req.method === 'POST' && req.body && Object.keys(req.body).length > 0;
    if (sendsJson) headers['content-type'] = 'application/json';
    const upstream = await fetch(new URL(`/api/customers/auth${route}`, apiBaseUrl), {
      method: req.method,
      headers,
      body: sendsJson ? JSON.stringify(req.body) : undefined,
      redirect: 'manual',
    });
    res.status(upstream.status);
    for (const name of ['content-type', 'retry-after']) {
      const value = upstream.headers.get(name);
      if (value) res.setHeader(name, value);
    }
    const cookies = upstream.headers.getSetCookie();
    if (cookies.length > 0) res.setHeader('set-cookie', cookies);
    res.setHeader('cache-control', 'private, no-store');
    res.send(Buffer.from(await upstream.arrayBuffer()));
  } catch {
    res.status(502).json({ title: 'The storefront could not reach the API.' });
  }
});


/**
 * The shopper's basket (cart C7, C10). The browser asks this origin (`/customer-api/cart...`) and the server forwards to `/api/cart...`,
 * carrying the cookies both ways, so the `romp-guest` cookie and the customer session belong to the storefront origin. Only the routes
 * below are reachable, each with the methods it accepts. The browser's `Origin` goes through unchanged (the API checks it); the real client
 * address goes in `x-forwarded-for`. Answers are never cached.
 */
const cartRoute = /^\/customer-api\/cart(\/lines(\/[0-9]+)?|\/discount)?$/;
const cartMethods: Readonly<Record<string, readonly string[]>> = {
  '/customer-api/cart': ['GET'],
  '/customer-api/cart/lines': ['POST'],
  '/customer-api/cart/discount': ['POST', 'DELETE'],
};

app.all('/customer-api/cart{/*splat}', express.json({ limit: '2kb' }), async (req, res) => {
  const withId = /^\/customer-api\/cart\/lines\/[0-9]+$/.test(req.path);
  const allowed = withId ? ['PUT', 'DELETE'] : (cartMethods[req.path] ?? []);
  if (!cartRoute.test(req.path) || !allowed.includes(req.method)) {
    res.status(404).end();
    return;
  }
  try {
    const headers: Record<string, string> = {};
    for (const name of ['cookie', 'origin', 'user-agent']) {
      const value = req.header(name);
      if (value) headers[name] = value;
    }
    const forwardedFor = [req.header('x-forwarded-for'), req.socket.remoteAddress].filter(Boolean).join(', ');
    if (forwardedFor) headers['x-forwarded-for'] = forwardedFor;
    const sendsJson = (req.method === 'POST' || req.method === 'PUT') && req.body && Object.keys(req.body).length > 0;
    if (sendsJson) headers['content-type'] = 'application/json';
    const upstream = await fetch(new URL(req.path.replace('/customer-api/cart', '/api/cart'), apiBaseUrl), {
      method: req.method,
      headers,
      body: sendsJson ? JSON.stringify(req.body) : undefined,
      redirect: 'manual',
    });
    res.status(upstream.status);
    const type = upstream.headers.get('content-type');
    if (type) res.setHeader('content-type', type);
    const cookies = upstream.headers.getSetCookie();
    if (cookies.length > 0) res.setHeader('set-cookie', cookies);
    res.setHeader('cache-control', 'private, no-store');
    res.send(Buffer.from(await upstream.arrayBuffer()));
  } catch {
    res.status(502).json({ title: 'The storefront could not reach the API.' });
  }
});

/**
 * Checkout (orders H1). The browser asks this origin (`/customer-api/orders...`) and the server forwards to `/api/orders...`, carrying the
 * cookies, the browser's `Origin` and the `Idempotency-Key` header, and the query string of the summary. Only the routes below are reachable,
 * each with the methods it accepts. Answers are never cached.
 */
const ordersMethods: Readonly<Record<string, readonly string[]>> = {
  '/customer-api/orders/checkout': ['GET'],
  '/customer-api/orders/payment-methods': ['GET'],
  '/customer-api/orders': ['POST'],
};

app.all('/customer-api/orders{/*splat}', express.json({ limit: '9kb' }), async (req, res) => {
  const allowed = ordersMethods[req.path] ?? [];
  if (!allowed.includes(req.method)) {
    res.status(404).end();
    return;
  }
  try {
    const headers: Record<string, string> = {};
    for (const name of ['cookie', 'origin', 'user-agent', 'idempotency-key']) {
      const value = req.header(name);
      if (value) headers[name] = value;
    }
    const forwardedFor = [req.header('x-forwarded-for'), req.socket.remoteAddress].filter(Boolean).join(', ');
    if (forwardedFor) headers['x-forwarded-for'] = forwardedFor;
    const sendsJson = req.method === 'POST' && req.body && Object.keys(req.body).length > 0;
    if (sendsJson) headers['content-type'] = 'application/json';
    const target = new URL(req.path.replace('/customer-api/orders', '/api/orders'), apiBaseUrl);
    target.search = new URL(req.originalUrl, apiBaseUrl).search;
    const upstream = await fetch(target, {
      method: req.method,
      headers,
      body: sendsJson ? JSON.stringify(req.body) : undefined,
      redirect: 'manual',
    });
    res.status(upstream.status);
    const type = upstream.headers.get('content-type');
    if (type) res.setHeader('content-type', type);
    const cookies = upstream.headers.getSetCookie();
    if (cookies.length > 0) res.setHeader('set-cookie', cookies);
    res.setHeader('cache-control', 'private, no-store');
    res.send(Buffer.from(await upstream.arrayBuffer()));
  } catch {
    res.status(502).json({ title: 'The storefront could not reach the API.' });
  }
});
/**
 * What the sign-in pages need to know at run time: the public Cloudflare Turnstile site key (empty in local runs, where the API accepts a
 * fixed development token instead). The site key is public by design; the secret key stays in the API.
 */
app.get('/customer-api/config', (_req, res) => {
  res.setHeader('cache-control', 'public, max-age=300');
  res.json({ turnstileSiteKey: process.env['TURNSTILE_SITE_KEY'] ?? '' });
});

/** The province and city lists behind the address form: public, anonymous, GET only. */
app.get('/api/geography/{*splat}', async (req, res) => {
  try {
    const upstream = await fetch(new URL(req.originalUrl, apiBaseUrl), { redirect: 'manual' });
    res.status(upstream.status);
    for (const name of ['content-type', 'etag', 'cache-control']) {
      const value = upstream.headers.get(name);
      if (value) res.setHeader(name, value);
    }
    res.send(Buffer.from(await upstream.arrayBuffer()));
  } catch {
    res.status(502).json({ title: 'The storefront could not reach the API.' });
  }
});

/**
 * Product picture files (hashed names, never change for the same bytes) come from the API's file store. Behind nginx in production the
 * same `/media` location serves them statically; here the SSR server forwards the one validated name.
 */
app.get('/media/:name', async (req, res) => {
  const name = req.params.name;
  if (!/^[0-9a-f]{32}-[0-9]{1,4}\.(webp|jpg)$/.test(name)) {
    res.status(404).end();
    return;
  }
  try {
    const upstream = await fetch(new URL(`/media/${name}`, apiBaseUrl), { redirect: 'manual' });
    res.status(upstream.status);
    const type = upstream.headers.get('content-type');
    if (type) res.setHeader('content-type', type);
    if (upstream.ok) res.setHeader('cache-control', 'public, max-age=31536000, immutable');
    res.send(Buffer.from(await upstream.arrayBuffer()));
  } catch {
    res.status(502).end();
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
