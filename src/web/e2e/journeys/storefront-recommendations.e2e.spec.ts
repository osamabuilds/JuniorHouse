import AxeBuilder from '@axe-core/playwright';
import { APIRequestContext, expect, test } from '@playwright/test';

/**
 * Product recommendations (SCRUM-218, REC-1, 4, 9, 11, 14) against the rebuilt storefront and API, with the demo shop: pieces in several age
 * groups, orders in the last two weeks and one cancelled. The worker has to have run once (it starts 30 seconds after the API).
 * Run: `docker compose -f docker-compose.yml -f docker-compose.secure.yml up -d --build`, wait a minute, then
 * `npx playwright test e2e/journeys/storefront-recommendations`.
 */
const BASE = process.env['STOREFRONT_BASE_URL'] ?? 'http://localhost:4000';
const API = process.env['API_BASE_URL'] ?? 'http://localhost:8080';
const AXE_TAGS = ['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'];

test.use({ baseURL: BASE });

interface Rows {
  similar: { path: string; productId: number }[];
  trending: { path: string; productId: number }[];
}

async function rowsOf(request: APIRequestContext, path: string): Promise<Rows | null> {
  const response = await request.get(`${API}/api/storefront/products/recommendations`, { params: { path } });
  return response.ok() ? ((await response.json()) as Rows) : null;
}

/** A demo product whose page has both rows, found through the listing and the API (the worker may still be filling the tables). */
async function productWithBothRows(request: APIRequestContext): Promise<string | null> {
  for (let attempt = 0; attempt < 12; attempt++) {
    const listing = await request.get(`${API}/api/storefront/listings`, { params: { scope: 'collection', collection: 'winter', category: 'jackets' } });
    const tiles = listing.ok() ? ((await listing.json()) as { tiles: { path: string }[] }).tiles : [];
    for (const tile of tiles) {
      const rows = await rowsOf(request, tile.path);
      if (rows && rows.similar.length > 0 && rows.trending.length > 0) return tile.path;
    }
    await new Promise((resolve) => setTimeout(resolve, 10_000));
  }
  return null;
}

test.describe('recommendations on a product page', () => {
  let path: string | null = null;

  test.beforeAll(async ({ request }) => {
    test.setTimeout(150_000);
    path = await productWithBothRows(request);
  });

  test.beforeEach(() => {
    test.skip(path === null, 'no demo product has both rows yet (has the worker run, and is Demo__SeedSampleData on?)');
  });

  test('REC-9 the server HTML has both rows as labelled sections with real links, with scripting off', async ({ browser }) => {
    const context = await browser.newContext({ javaScriptEnabled: false });
    const page = await context.newPage();
    const response = await page.goto(path!);
    expect(response!.status()).toBe(200);

    await expect(page.locator('h1')).toHaveCount(1);
    for (const name of ['You may also like', 'Trending now']) {
      const section = page.locator('section', { has: page.getByRole('heading', { level: 2, name }) });
      await expect(section).toHaveCount(1);
      const links = section.locator('ul.rp-rail li h3 a[href^="/"]');
      expect(await links.count()).toBeGreaterThan(0);
      expect(await links.first().textContent()).toBeTruthy();
    }
    await context.close();
  });

  for (const width of [390, 768, 1280]) {
    test(`REC-11 at ${width}px the page does not scroll sideways and the rows cause no layout shift`, async ({ page }) => {
      await page.setViewportSize({ width, height: 900 });
      await page.addInitScript(() => {
        (window as unknown as { __cls: number }).__cls = 0;
        new PerformanceObserver((list) => {
          for (const entry of list.getEntries() as unknown as { value: number; hadRecentInput: boolean }[]) {
            if (!entry.hadRecentInput) (window as unknown as { __cls: number }).__cls += entry.value;
          }
        }).observe({ type: 'layout-shift', buffered: true });
      });
      await page.goto(path!);
      await expect(page.getByRole('heading', { level: 2, name: 'You may also like' })).toBeVisible();
      await page.getByRole('heading', { level: 2, name: 'Trending now' }).scrollIntoViewIfNeeded();
      await page.waitForTimeout(1500);

      expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
      expect(await page.evaluate(() => (window as unknown as { __cls: number }).__cls)).toBeLessThan(0.1);
    });
  }

  test('REC-14 by keyboard alone: reach a card, choose a size and add it', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    await page.goto(path!);
    const rail = page.locator('rp-product-recommendations ul.rp-rail').first();
    await rail.scrollIntoViewIfNeeded();
    await expect(rail).toHaveAttribute('tabindex', '0');

    await rail.locator('li h3 a').first().focus();
    const outline = await rail.locator('li h3 a').first().evaluate((el) => getComputedStyle(el).outlineStyle);
    expect(outline).not.toBe('none');

    let reached = false;
    for (let i = 0; i < 40 && !reached; i++) {
      await page.keyboard.press('Tab');
      reached = await page.evaluate(() => !!document.activeElement?.matches('.rp-qa__sizes button:not([disabled]):not([aria-disabled="true"])'));
    }
    expect(reached, 'an available size button of a card is reachable with Tab').toBe(true);
    await page.keyboard.press('Enter');
    await expect(page.locator('rp-product-recommendations [role="status"]').filter({ hasText: /Added/ }).first()).toBeVisible();
  });

  test('REC-14 the product page with both rows has no automated accessibility violations', async ({ page }) => {
    await page.goto(path!);
    await expect(page.getByRole('heading', { level: 2, name: 'Trending now' })).toBeVisible();
    const axe = await new AxeBuilder({ page }).withTags(AXE_TAGS).analyze();
    expect(axe.violations.map((v) => v.id)).toEqual([]);
  });

  test('REC-12 using a card link sends one event to the counter endpoint (body shape: unit test)', async ({ page, request }) => {
    const seen: string[] = [];
    page.on('request', (r) => {
      if (r.url().endsWith('/api/storefront/products/recommendations/events')) seen.push(r.postDataBuffer()?.toString() ?? '');
    });
    await page.goto(path!);
    const link = page.locator('rp-product-recommendations').first().locator('li h3 a').first();
    await link.scrollIntoViewIfNeeded();
    await link.click();
    await expect.poll(() => seen.length).toBeGreaterThan(0);

    expect((await request.get(`${API}/health/live`)).ok()).toBe(true);
  });
});

test('REC-1 an unknown product path has no recommendations and is a 404, never a 500', async ({ request }) => {
  const response = await request.get(`${API}/api/storefront/products/recommendations`, { params: { path: '/girls/dresses/zz-nothing' } });
  expect(response.status()).toBe(404);
});
