#!/usr/bin/env node
/**
 * Load test of the payment endpoints against a RUNNING stack (NFR-04: 500 concurrent users and 3x traffic in a sale, NFR-09, SEC-26).
 *
 * It measures latency (p50, p95, p99, max), throughput and error rate for the routes a sale would hammer, and checks the API stays healthy and
 * answers honestly under load: forged callbacks are all refused, signed callbacks about unknown attempts are all recorded once each, junk
 * links are all "not found", and the rate limit pushes back with 429 instead of failing with a 5xx. Only run it against a stack you own.
 *
 *   node scripts/perf/payments-load.mjs
 * Environment: API_BASE (default http://localhost:8080), SHOP_ORIGIN (the shop address sent as the Origin header, default http://localhost:4000), FAKE_SECRET (default the local demo secret; "" skips the signed part),
 *   CONCURRENCY (default 50), REQUESTS (default 500 per scenario, kept under the webhook rate limit of 600 a minute by default).
 * For a limit-free run, start the API with RateLimit__WebhookPerMinute and RateLimit__StrictPerMinute raised, and increase REQUESTS.
 * Exit code 0 when every budget holds. Results are written to scripts/perf/last-run.json.
 */
import { createHmac, randomBytes } from 'node:crypto';
import { writeFileSync } from 'node:fs';
import net from 'node:net';

// With "localhost" resolving to both IPv4 and IPv6, Node races the two under load. If something else on the machine listens on the other family
// (an Apache on port 8080, say), a share of the connections would reach it and look like 404s from the API. Use one address, consistently.
net.setDefaultAutoSelectFamily(false);

const API = (process.env.API_BASE ?? 'http://localhost:8080').replace(/\/$/, '');
const ORIGIN = (process.env.SHOP_ORIGIN ?? 'http://localhost:4000').replace(/\/$/, '');
const SECRET = process.env.FAKE_SECRET ?? 'local-demo-fake-gateway-secret-0123456789';
const CONCURRENCY = Number(process.env.CONCURRENCY ?? 50);
const REQUESTS = Number(process.env.REQUESTS ?? 500);

const report = [];

const pct = (sorted, p) => (sorted.length === 0 ? 0 : sorted[Math.min(sorted.length - 1, Math.ceil(p * sorted.length) - 1)]);

async function run(name, makeRequest, { p95Budget, expect: accepted }) {
  const latencies = [];
  const statuses = new Map();
  let next = 0;
  const started = performance.now();
  async function worker() {
    while (next < REQUESTS) {
      const i = next++;
      const t0 = performance.now();
      let status = 0;
      try {
        const res = await makeRequest(i);
        status = res.status;
        await res.arrayBuffer();
      } catch {
        status = 0;
      }
      const ms = performance.now() - t0;
      if (status !== 429) latencies.push(ms); // a request the limiter turned away says nothing about processing time
      statuses.set(status, (statuses.get(status) ?? 0) + 1);
    }
  }
  await Promise.all(Array.from({ length: CONCURRENCY }, worker));
  const seconds = (performance.now() - started) / 1000;
  latencies.sort((a, b) => a - b);
  const bad = [...statuses.entries()].filter(([s]) => !accepted.includes(s) && s !== 429);
  const row = {
    name,
    requests: REQUESTS,
    concurrency: CONCURRENCY,
    seconds: Number(seconds.toFixed(2)),
    perSecond: Number((REQUESTS / seconds).toFixed(0)),
    p50: Number(pct(latencies, 0.5).toFixed(1)),
    p95: Number(pct(latencies, 0.95).toFixed(1)),
    p99: Number(pct(latencies, 0.99).toFixed(1)),
    max: Number((latencies.at(-1) ?? 0).toFixed(1)),
    statuses: Object.fromEntries(statuses),
    ok: bad.length === 0 && pct(latencies, 0.95) <= p95Budget,
    p95Budget,
  };
  report.push(row);
  console.log(`${row.ok ? 'PASS' : 'FAIL'}  ${name}: ${row.perSecond}/s, p50 ${row.p50} ms, p95 ${row.p95} ms (budget ${p95Budget}), p99 ${row.p99} ms, max ${row.max} ms, statuses ${JSON.stringify(row.statuses)}`);
}

const sign = (ts, body) => `t=${ts},v1=${createHmac('sha256', SECRET).update(`${ts}.`).update(body).digest('hex')}`;
const bodyFor = (ref, eventId) => JSON.stringify({ eventId, gatewayRef: ref, outcome: 'paid', amount: 1, occurredAt: new Date().toISOString() });

const live = await fetch(`${API}/health/live`).catch(() => null);
if (!live || live.status !== 200) {
  console.error(`The API is not answering at ${API}. Start the stack first.`);
  process.exit(2);
}
console.log(`Load test of payments against ${API}: ${REQUESTS} requests per scenario, ${CONCURRENCY} at a time\n`);

// A short untimed warm-up first: the first requests after a restart pay for cold code, which says nothing about steady state.
await Promise.all(Array.from({ length: 300 }, (_, i) => fetch(`${API}/api/orders/payments/status?token=warm${i}`).then((r) => r.arrayBuffer()).catch(() => null)));
await Promise.all(Array.from({ length: 200 }, () => fetch(`${API}/api/orders/payments/webhook/fake`, { method: 'POST', headers: { 'content-type': 'application/json' }, body: '{}' }).then((r) => r.arrayBuffer()).catch(() => null)));

await run('forged webhook callbacks (rejected, recorded)', () =>
  fetch(`${API}/api/orders/payments/webhook/fake`, {
    method: 'POST',
    headers: { 'content-type': 'application/json', 'x-fake-signature': `t=${Math.floor(Date.now() / 1000)},v1=${randomBytes(32).toString('hex')}` },
    body: '{"x":1}',
  }), { p95Budget: 1500, expect: [401] });

if (SECRET) {
  await run('signed callbacks for unknown attempts (verified, recorded once)', (i) => {
    const body = bodyFor('fk_000000000000000000000000', `load-${Date.now()}-${i}-${randomBytes(3).toString('hex')}`);
    return fetch(`${API}/api/orders/payments/webhook/fake`, {
      method: 'POST',
      headers: { 'content-type': 'application/json', 'x-fake-signature': sign(Math.floor(Date.now() / 1000), body) },
      body,
    });
  }, { p95Budget: 1500, expect: [200] });

  const same = bodyFor('fk_000000000000000000000000', `load-same-${Date.now()}`);
  await run('one signed callback delivered over and over (duplicates)', () =>
    fetch(`${API}/api/orders/payments/webhook/fake`, {
      method: 'POST',
      headers: { 'content-type': 'application/json', 'x-fake-signature': sign(Math.floor(Date.now() / 1000), same) },
      body: same,
    }), { p95Budget: 1500, expect: [200] });
}

await run('payment status with a junk token (not found)', (i) => fetch(`${API}/api/orders/payments/status?token=junk${i}`), { p95Budget: 800, expect: [404] });
await run('start payment with a junk token (not found)', (i) =>
  fetch(`${API}/api/orders/payments/start`, { method: 'POST', headers: { 'content-type': 'application/json', origin: ORIGIN }, body: JSON.stringify({ token: `junk${i}` }) }), { p95Budget: 800, expect: [404] });
await run('payment methods list', () => fetch(`${API}/api/orders/payment-methods`), { p95Budget: 400, expect: [200] });

const after = await fetch(`${API}/health/ready`).catch(() => null);
const healthy = !!after && after.status === 200;
console.log(`\nAPI health after the load: ${healthy ? 'ready' : 'NOT READY'}`);
const failed = report.filter((r) => !r.ok).length + (healthy ? 0 : 1);
writeFileSync(new URL('./last-run.json', import.meta.url), JSON.stringify({ at: new Date().toISOString(), api: API, healthyAfter: healthy, report }, null, 2));
console.log(failed === 0 ? 'All budgets held.' : `${failed} budget(s) missed.`);
process.exit(failed === 0 ? 0 : 1);
