import {
  AngularNodeAppEngine,
  createNodeRequestHandler,
  isMainModule,
  writeResponseToNodeResponse,
} from '@angular/ssr/node';
import express from 'express';
import { join } from 'node:path';
import { cacheControlFor } from './static-cache';

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

/** The wishlist (WSL-*): the list, the ids for the heart state, add and remove by product id, and the guest merge. */
const wishlist = /^\/customer-api\/wishlist(\/(ids|merge|[0-9]+))?$/;

app.all('/customer-api/{*splat}', express.json({ limit: '16kb' }), async (req, res, next) => {
  if (req.path === '/customer-api/config' || req.path === '/customer-api/newsletter' || req.path.startsWith('/customer-api/newsletter/') || req.path === '/customer-api/notify-me' || req.path.startsWith('/customer-api/auth/') || req.path === '/customer-api/cart' || req.path.startsWith('/customer-api/cart/') || req.path === '/customer-api/orders' || req.path.startsWith('/customer-api/orders/') || req.path === '/customer-api/shipping/track') {
    next(); // the sign-in routes below
    return;
  }
  const isWishlist = wishlist.test(req.path);
  if ((!addressBook.test(req.path) && !isWishlist) || !['GET', 'POST', 'PUT', 'DELETE'].includes(req.method)) {
    res.status(404).end();
    return;
  }
  try {
    const headers: Record<string, string> = {};
    const cookie = req.header('cookie');
    if (cookie) headers['cookie'] = cookie;
    const sendsJson = (req.method === 'POST' || req.method === 'PUT') && req.body && Object.keys(req.body).length > 0;
    if (sendsJson) headers['content-type'] = 'application/json';
    const target = new URL(
      isWishlist ? req.path.replace('/customer-api/wishlist', '/api/customers/me/wishlist') : req.path.replace('/customer-api/addresses', '/api/customers/me/addresses'),
      apiBaseUrl,
    );
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
 * Newsletter consent (customers/newsletter NLS-1 to NLS-6). The browser asks this origin (`/customer-api/newsletter...`) and the server
 * forwards to the API with the cookies, the browser's `Origin` (the API checks it on subscribe) and the real client address. Only the
 * routes below are reachable, each with the methods it accepts. Answers are never cached.
 */
const newsletterRoutes: Readonly<Record<string, { readonly methods: readonly string[]; readonly target: string }>> = {
  '/customer-api/newsletter/subscribe': { methods: ['POST'], target: '/api/newsletter/subscribe' },
  '/customer-api/newsletter/unsubscribe': { methods: ['GET', 'POST'], target: '/api/newsletter/unsubscribe' },
  '/customer-api/newsletter': { methods: ['GET', 'PUT'], target: '/api/customers/me/newsletter' },
};

/** What the API said, as the result page understands it: 2xx done, 4xx a request that cannot be used, anything else an error. */
const outcome = (status: number): 'ok' | 'invalid' | 'error' => (status >= 200 && status < 300 ? 'ok' : status >= 400 && status < 500 && status !== 429 && status !== 403 ? 'invalid' : 'error');

const clientHeaders = (req: express.Request): Record<string, string> => {
  const headers: Record<string, string> = {};
  for (const name of ['cookie', 'origin', 'user-agent']) {
    const value = req.header(name);
    if (value) headers[name] = value;
  }
  const forwardedFor = [req.header('x-forwarded-for'), req.socket.remoteAddress].filter(Boolean).join(', ');
  if (forwardedFor) headers['x-forwarded-for'] = forwardedFor;
  return headers;
};

app.all('/customer-api/newsletter{/*splat}', express.json({ limit: '2kb' }), async (req, res) => {
  const route = newsletterRoutes[req.path];
  if (!route?.methods.includes(req.method)) {
    res.status(404).end();
    return;
  }
  try {
    const headers = clientHeaders(req);
    const sendsJson = (req.method === 'POST' || req.method === 'PUT') && req.body && Object.keys(req.body).length > 0;
    if (sendsJson) headers['content-type'] = 'application/json';
    const target = new URL(route.target, apiBaseUrl);
    target.search = new URL(req.originalUrl, apiBaseUrl).search;
    const upstream = await fetch(target, { method: req.method, headers, body: sendsJson ? JSON.stringify(req.body) : undefined, redirect: 'manual' });
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
 * Notify Me (customers/notify-me NTM-1 to NTM-10). The browser posts `{ variantId, email }` to `/customer-api/notify-me` and the server
 * forwards it to `/api/notify-me` with the cookies, the browser `Origin` (the API checks it) and the real client address. The answer is
 * passed back as it is (a 400 carries the reason) and is never cached.
 */
app.post('/customer-api/notify-me', express.json({ limit: '2kb' }), async (req, res) => {
  try {
    const upstream = await fetch(new URL('/api/notify-me', apiBaseUrl), {
      method: 'POST',
      headers: { ...clientHeaders(req), 'content-type': 'application/json' },
      body: JSON.stringify({ variantId: req.body?.variantId, email: req.body?.email }),
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
 * The Notify Me form without JavaScript: the browser posts the size and the email here, the server records the request and sends the
 * visitor on (303) to a page that says what happened. With JavaScript the same form is handled in the page and never reaches this route.
 */
app.post('/notify-me', express.urlencoded({ extended: false, limit: '2kb' }), async (req, res) => {
  let status: 'ok' | 'invalid' | 'error' = 'error';
  try {
    const variantId = Number.parseInt(typeof req.body?.variantId === 'string' ? req.body.variantId : '', 10);
    const email = typeof req.body?.email === 'string' ? req.body.email : '';
    const upstream = await fetch(new URL('/api/notify-me', apiBaseUrl), {
      method: 'POST',
      headers: { ...clientHeaders(req), 'content-type': 'application/json' },
      body: JSON.stringify({ variantId: Number.isSafeInteger(variantId) ? variantId : 0, email }),
      redirect: 'manual',
    });
    status = outcome(upstream.status);
  } catch {
    status = 'error';
  }
  res.setHeader('cache-control', 'private, no-store');
  res.redirect(303, `/notify-me/requested?status=${status}`);
});

/**
 * The footer form without JavaScript: the browser posts the email here, the server subscribes it and sends the visitor on (303) to a page
 * that says what happened. With JavaScript the same form is handled in the page and never reaches this route.
 */
app.post('/newsletter/subscribe', express.urlencoded({ extended: false, limit: '2kb' }), async (req, res) => {
  let status: 'ok' | 'invalid' | 'error' = 'error';
  try {
    const email = typeof req.body?.email === 'string' ? req.body.email : '';
    const upstream = await fetch(new URL('/api/newsletter/subscribe', apiBaseUrl), {
      method: 'POST',
      headers: { ...clientHeaders(req), 'content-type': 'application/json' },
      body: JSON.stringify({ email }),
      redirect: 'manual',
    });
    status = outcome(upstream.status);
  } catch {
    status = 'error';
  }
  res.setHeader('cache-control', 'private, no-store');
  res.redirect(303, `/newsletter/subscribed?status=${status}`);
});

/**
 * The link in a newsletter email. Opening it unsubscribes (idempotent; the signed token names one record), then the visitor is sent on to a
 * page that says so. A mail scanner that opens the link also unsubscribes: accepted, it errs towards not mailing.
 */
app.get('/newsletter/unsubscribe', async (req, res) => {
  let status: 'ok' | 'invalid' | 'error' = 'error';
  try {
    const token = typeof req.query['token'] === 'string' ? req.query['token'].slice(0, 200) : '';
    const target = new URL('/api/newsletter/unsubscribe', apiBaseUrl);
    target.searchParams.set('token', token);
    const upstream = await fetch(target, { headers: clientHeaders(req), redirect: 'manual' });
    status = outcome(upstream.status);
  } catch {
    status = 'error';
  }
  res.setHeader('cache-control', 'private, no-store');
  res.redirect(303, `/newsletter/unsubscribed?status=${status}`);
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
 * Track a parcel by its tracking number (shipping tracking TRK-9, TRK-10). The browser asks `/customer-api/shipping/track?waybill=...` and the
 * server forwards to `/api/shipping/track` with the real client address, so the API's rate limit and CAPTCHA rules see the visitor and not this
 * server. Only this one GET is reachable. Answers are never cached.
 */
app.get('/customer-api/shipping/track', async (req, res) => {
  try {
    const headers: Record<string, string> = {};
    const forwardedFor = [req.header('x-forwarded-for'), req.socket.remoteAddress].filter(Boolean).join(', ');
    if (forwardedFor) headers['x-forwarded-for'] = forwardedFor;
    const target = new URL('/api/shipping/track', apiBaseUrl);
    target.search = new URL(req.originalUrl, apiBaseUrl).search;
    const upstream = await fetch(target, { method: 'GET', headers, redirect: 'manual' });
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
 * Checkout (orders H1). The browser asks this origin (`/customer-api/orders...`) and the server forwards to `/api/orders...`, carrying the
 * cookies, the browser's `Origin` and the `Idempotency-Key` header, and the query string of the summary. Only the routes below are reachable,
 * each with the methods it accepts. Answers are never cached.
 */
const ordersMethods: Readonly<Record<string, readonly string[]>> = {
  '/customer-api/orders/checkout': ['GET'],
  '/customer-api/orders/payment-methods': ['GET'],
  '/customer-api/orders/cod/status': ['GET'],
  '/customer-api/orders/cod/confirm': ['POST'],
  '/customer-api/orders/cod/cancel': ['POST'],
  '/customer-api/orders/lookup': ['POST'],
  '/customer-api/orders/lookup/link': ['GET'],
  // Online payment (orders payments Y3): start or reopen the hosted page, and follow the result. The gateway's own webhook is never reachable
  // from the browser origin: it talks to the API directly.
  '/customer-api/orders/payments/start': ['POST'],
  '/customer-api/orders/payments/status': ['GET'],
  '/customer-api/orders': ['POST'],
};

/**
 * The pretend payment page of the fake gateway (local stack only; the API maps it only when the fake gateway is on, and refuses to start
 * with it anywhere else). It lives on this origin so the browser can reach it and come back with its cookies. Exactly one shape is allowed.
 */
const fakePaymentPage = /^\/customer-api\/orders\/payments\/fake\/fk_[0-9a-f]{24}(\/complete)?$/;

app.all('/customer-api/orders{/*splat}', express.json({ limit: '9kb' }), express.urlencoded({ extended: false, limit: '1kb' }), async (req, res) => {
  const allowed = ordersMethods[req.path] ?? (fakePaymentPage.test(req.path) ? (req.path.endsWith('/complete') ? ['POST'] : ['GET']) : []);
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
    const sendsJson = req.method === 'POST' && req.is('application/json') !== false && req.body && Object.keys(req.body).length > 0;
    const sendsForm = req.method === 'POST' && !!req.is('application/x-www-form-urlencoded') && req.body && Object.keys(req.body).length > 0;
    if (sendsJson) headers['content-type'] = 'application/json';
    if (sendsForm) headers['content-type'] = 'application/x-www-form-urlencoded';
    const target = new URL(req.path.replace('/customer-api/orders', '/api/orders'), apiBaseUrl);
    target.search = new URL(req.originalUrl, apiBaseUrl).search;
    const upstream = await fetch(target, {
      method: req.method,
      headers,
      body: sendsJson ? JSON.stringify(req.body) : sendsForm ? new URLSearchParams(req.body as Record<string, string>).toString() : undefined,
      redirect: 'manual',
    });
    res.status(upstream.status);
    const type = upstream.headers.get('content-type');
    if (type) res.setHeader('content-type', type);
    // Only the pretend payment page redirects, and only back to a path on this shop: never to anywhere else the API might name.
    const location = upstream.headers.get('location');
    if (location && location.startsWith('/') && !location.startsWith('//')) {
      res.setHeader('location', location);
    } else if (location) {
      // The API may name the shop by its public address: that is allowed only when it is this very host, and it is sent on as a plain path.
      try {
        const target = new URL(location);
        if ((target.protocol === 'http:' || target.protocol === 'https:') && target.host === req.get('host')) res.setHeader('location', `${target.pathname}${target.search}`);
      } catch {
        // not an address: no redirect
      }
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
    etag: true,
    setHeaders: (res, filePath) => res.setHeader('Cache-Control', cacheControlFor(filePath)),
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
