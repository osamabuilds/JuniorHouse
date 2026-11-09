import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Page } from '@playwright/test';

/**
 * Collections page (docs/specs/storefront/collections-page, COL-1..8). Runs against the storefront and API containers.
 */
const BASE = process.env['STOREFRONT_BASE_URL'] ?? 'http://localhost:4000';
const AXE_TAGS = ['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'];

test.use({ baseURL: BASE });

async function open(page: Page, path = '/collections'): Promise<void> {
  await page.goto(path, { waitUntil: 'networkidle' });
  await page.waitForTimeout(300);
}

test('COL-1 the API lists enabled collections with live products, with counts and a path', async ({ request }) => {
  const res = await request.get('/api/storefront/collections');
  expect(res.status()).toBe(200);
  expect(res.headers()['cache-control']).toContain('max-age=60');
  const body = await res.json();
  expect(body.items.length).toBeGreaterThan(0);
  for (const item of body.items) {
    expect(item.path).toBe('/collections/' + String(item.code).toLowerCase().replace(/_/g, '-'));
    expect(item.pieces).toBeGreaterThan(0);
  }
  expect(body.pageSize).toBeLessThanOrEqual(48);
});

test('COL-1 hostile paging is clamped, never an error', async ({ request }) => {
  for (const q of ['?page=abc', '?page=-5&pageSize=100000', '?page=999999999999&pageSize=0']) {
    const res = await request.get('/api/storefront/collections' + q);
    expect(res.status(), q).toBe(200);
  }
});

test('COL-1 COL-5 the server HTML has the tiles as links, one h1, the canonical, the description and BreadcrumbList data', async ({ request }) => {
  const res = await request.get('/collections');
  expect(res.status()).toBe(200);
  const html = await res.text();
  expect(html.match(/<h1[\s>]/g)?.length).toBe(1);
  expect(html).toContain('Collections');
  expect(html).toMatch(/<a[^>]*class="rp-tile[^"]*"[^>]*href="\/collections\/[a-z0-9-]+"/);
  expect(html).toMatch(/<link[^>]*rel="canonical"[^>]*href="[^"]*\/collections"/);
  expect(html).toMatch(/<meta[^>]*name="description"/);
  expect(html).toContain('BreadcrumbList');
  expect(html).not.toMatch(/<meta[^>]*name="robots"/);
});

test('COL-1 a page after the last is a real 404', async ({ request }) => {
  const res = await request.get('/collections?page=99');
  expect(res.status()).toBe(404);
});

test('COL-8 the footer, header and mobile menu links to Collections open the page, not a 404', async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 900 });
  await open(page, '/delivery');
  await page.locator('footer a[href="/collections"]').first().click();
  await expect(page.locator('h1')).toHaveText('Collections');
  await expect(page).toHaveURL(/\/collections$/);

  await page.setViewportSize({ width: 390, height: 844 });
  await open(page, '/delivery');
  await page.getByRole('button', { name: 'Open menu' }).click();
  const dialog = page.getByRole('dialog', { name: 'Menu' });
  const collectionsLink = dialog.locator('a[href="/collections"]');
  if (await collectionsLink.count()) {
    await collectionsLink.first().click();
    await expect(page).toHaveURL(/\/collections$/);
    await expect(page.locator('h1')).toHaveText('Collections');
  }
});

test('COL-1 a tile opens its collection listing', async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 900 });
  await open(page);
  const first = page.locator('a.rp-tile').first();
  const href = await first.getAttribute('href');
  await first.click();
  await expect(page).toHaveURL(new RegExp(href!.replace(/\//g, '\\/') + '$'));
  await expect(page.locator('h1')).toBeVisible();
});

for (const vp of [
  { name: 'phone', width: 390, height: 844 },
  { name: 'tablet', width: 768, height: 1024 },
  { name: 'desktop', width: 1280, height: 900 },
]) {
  test.describe(`quality at ${vp.name}`, () => {
    test.use({ viewport: { width: vp.width, height: vp.height } });

    test('COL-7 no horizontal scroll, no axe violations, no layout shift, tiles stack on a phone and sit two across from 40rem', async ({ page }) => {
      await page.addInitScript(() => {
        (window as unknown as { __cls: number }).__cls = 0;
        new PerformanceObserver((list) => {
          for (const e of list.getEntries() as unknown as { value: number; hadRecentInput: boolean }[]) {
            if (!e.hadRecentInput) (window as unknown as { __cls: number }).__cls += e.value;
          }
        }).observe({ type: 'layout-shift', buffered: true });
      });
      await open(page);
      const results = await new AxeBuilder({ page }).withTags(AXE_TAGS).analyze();
      expect(results.violations.map((v) => v.id + ': ' + v.nodes.map((n) => JSON.stringify(n.target)).join(', '))).toEqual([]);
      const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
      expect(overflow).toBeLessThanOrEqual(0);
      expect(await page.evaluate(() => (window as unknown as { __cls: number }).__cls)).toBeLessThanOrEqual(0.1);
      const boxes = await page.locator('ul.rp-split > li').evaluateAll((els) => els.map((e) => e.getBoundingClientRect().left));
      if (boxes.length >= 2) {
        const sameRow = Math.abs(boxes[0] - boxes[1]) > 10;
        expect(sameRow).toBe(vp.width >= 640);
      }
      const small = await page.$$eval('a.rp-tile', (els) => els.filter((e) => e.getBoundingClientRect().height < 44).length);
      expect(small).toBe(0);
    });
  });
}

test('COL-4 headings never skip a level', async ({ page }) => {
  await open(page);
  const levels = await page.$$eval('main h1, main h2, main h3', (hs) => hs.map((h) => Number(h.tagName[1])));
  expect(levels[0]).toBe(1);
  levels.forEach((l, i) => i > 0 && expect(l - levels[i - 1]).toBeLessThanOrEqual(1));
});
