import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';

/**
 * Search (SCRUM-203, SRC-1..4, 8, 9, 11, 12) against the storefront container and the API behind it. The data-driven checks use the demo
 * product ("Floral Summer Dress", SKU ROMP-DEMO-FLORAL-RED-1-2Y); with none published they are skipped and the status-code checks still run.
 * Run: `docker compose up -d --build api storefront`, then `STOREFRONT_BASE_URL=http://localhost:4000 npx playwright test e2e/storefront/search`.
 */
const BASE = process.env['STOREFRONT_BASE_URL'] ?? 'http://localhost:4000';
const AXE_TAGS = ['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'];
const VIEWPORTS = [320, 375, 390, 768, 1024, 1280, 1440];
const DEMO = '/girls/dresses/floral-summer-dress';

test.use({ baseURL: BASE });

async function demoExists(request: import('@playwright/test').APIRequestContext): Promise<boolean> {
  return (await request.get(DEMO)).status() === 200;
}

test.describe('status codes and safety (no data needed)', () => {
  test('SRC-2 gibberish is a normal 200 page with a message and a way onward, not indexed', async ({ page }) => {
    const response = await page.goto('/search?q=qzxjvkw');
    expect(response!.status()).toBe(200);
    await expect(page.locator('h1')).toHaveCount(1);
    await expect(page.locator('.rp-empty h2')).toContainText('Nothing found for “qzxjvkw”');
    await expect(page.locator('.rp-empty a').first()).toBeVisible();
    await expect(page.locator('meta[name="robots"]')).toHaveAttribute('content', 'noindex, follow');
  });

  test('SRC-11 the canonical is the search itself, never paging or filters', async ({ page }) => {
    await page.goto('/search?q=dress&sort=price-asc');
    const canonical = await page.locator('link[rel="canonical"]').getAttribute('href');
    expect(canonical).toMatch(/\/search\?q=dress$/);
  });

  test('SRC-3 hostile text is shown as text and never runs', async ({ page }) => {
    let dialogs = 0;
    page.on('dialog', async (d) => {
      dialogs++;
      await d.dismiss();
    });
    for (const q of ['<img src=x onerror=alert(1)>', '<script>alert(1)</script>', "' OR 1=1 --", '& | ! ( ) :*']) {
      const response = await page.goto(`/search?q=${encodeURIComponent(q)}`);
      expect(response!.status(), q).toBe(200);
      const body = await page.content();
      expect(body, q).not.toMatch(/Exception|Npgsql|SELECT |   at /);
      await expect(page.locator('h1')).toHaveCount(1);
    }

    expect(dialogs).toBe(0);
    await expect(page.locator('img[src="x"]')).toHaveCount(0);
  });

  test('SRC-3 very long text and control characters are answered', async ({ request }) => {
    expect((await request.get(`/search?q=${'a'.repeat(5000)}`)).status()).toBeLessThan(400);
    expect((await request.get('/search?q=%00%E2%80%AE%E2%80%8Bdress')).status()).toBeLessThan(400);
  });

  test('SEO-06 untidy text or unknown keys get one clean address (301)', async ({ request }) => {
    const response = await request.get('/search?q=%20%20Floral%20%20DRESS&utm_source=x', { maxRedirects: 0 });
    expect(response.status()).toBe(301);
    expect(response.headers()['location']).toBe('/search?q=floral%20dress');
  });

  test('SRC-2 with nothing typed the page is just the search form', async ({ page }) => {
    const response = await page.goto('/search');
    expect(response!.status()).toBe(200);
    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Search');
    await expect(page.locator('#rp-search-q')).toBeVisible();
  });

  test('SRC-3 malformed numbers on the API are a 400, not a crash', async ({ request }) => {
    expect((await request.get('/api/storefront/search?q=dress&page=abc')).status()).toBe(400);
  });
});

test.describe('searching the demo shop', () => {
  test.beforeEach(async ({ request }) => {
    test.skip(!(await demoExists(request)), 'the demo product is not published');
  });

  test('SRC-1 a word finds the product, with the count, the tile and the filter bar', async ({ page }) => {
    const response = await page.goto('/search?q=dress');
    expect(response!.status()).toBe(200);
    await expect(page.locator('h1')).toContainText('Results for “dress”');
    await expect(page.locator('rp-product-tile', { hasText: 'Floral Summer Dress' })).toBeVisible();
    await expect(page.locator('rp-filter-panel')).toBeVisible();
    await expect(page.locator('rp-filter-panel input[type="hidden"][name="q"]')).toHaveValue('dress');
  });

  test('SRC-12 a synonym finds the same product (frock for dress)', async ({ page }) => {
    await page.goto('/search?q=frock');
    await expect(page.locator('rp-product-tile', { hasText: 'Floral Summer Dress' })).toBeVisible();
  });

  test('SRC-8 one slip is tolerated', async ({ page }) => {
    await page.goto('/search?q=florl');
    await expect(page.locator('rp-product-tile', { hasText: 'Floral Summer Dress' })).toBeVisible();
  });

  test('SRC-4 SRC-5 an exact SKU, in any case, opens the product with its colour and size selected', async ({ page }) => {
    await page.goto('/search?q=romp-demo-floral-red-1-2y');
    await expect(page).toHaveURL(/\/girls\/dresses\/floral-summer-dress\?colour=wht&size=1-2y/);
    await expect(page.locator('h1')).toContainText('Floral Summer Dress');
  });

  test('SRC-4 the SKU redirect is a real 302 from the server (a capitalised SKU is first tidied by a 301)', async ({ request }) => {
    const response = await request.get('/search?q=romp-demo-floral-red-1-2y', { maxRedirects: 0 });
    expect(response.status()).toBe(302);
    expect(response.headers()['location']).toContain('/girls/dresses/floral-summer-dress');
  });

  test('SRC-4 a style code opens the product', async ({ request }) => {
    const response = await request.get('/search?q=sty-dress-001', { maxRedirects: 0 });
    expect(response.status()).toBe(302);
    expect(response.headers()['location']).toBe('/girls/dresses/floral-summer-dress');
  });

  test('SRC-9 without scripting the form is a plain GET to /search', async ({ browser }) => {
    const context = await browser.newContext({ javaScriptEnabled: false, baseURL: BASE });
    const page = await context.newPage();
    await page.goto('/search?q=dress');
    await page.locator('#rp-search-q').fill('frock');
    await page.locator('main form[role="search"] button[type="submit"]').click();
    await expect(page).toHaveURL(/\/search\?q=frock/);
    await expect(page.locator('rp-product-tile', { hasText: 'Floral Summer Dress' })).toBeVisible();
    await context.close();
  });

  test('SRC-9 the header overlay suggests while typing and works from the keyboard', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 800 });
    await page.goto('/');
    await page.locator('.rp-search-btn').click();
    const field = page.getByRole('dialog', { name: 'Search' }).getByRole('searchbox');
    await field.fill('flor');
    const first = page.locator('.rp-suggestions a').first();
    await expect(first).toContainText('Floral Summer Dress');
    await expect(first).toHaveAttribute('href', DEMO);
    await field.press('ArrowDown');
    await expect(first).toBeFocused();
    await page.keyboard.press('Escape');
    await expect(page.getByRole('dialog', { name: 'Search' })).toBeHidden();
  });

  test('SRC-9 pressing Enter in the overlay opens the results page', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 800 });
    await page.goto('/');
    await page.locator('.rp-search-btn').click();
    const field = page.getByRole('dialog', { name: 'Search' }).getByRole('searchbox');
    await field.fill('dress');
    await field.press('Enter');
    await expect(page).toHaveURL(/\/search\?q=dress/);
  });

  test('SRC-1 applying a filter keeps the search text', async ({ page }) => {
    await page.goto('/search?q=dress');
    await page.locator('rp-filter-panel summary').click();
    await page.locator('rp-filter-panel select[name="sort"]').selectOption('price-asc');
    await page.getByRole('button', { name: 'Apply' }).click();
    await expect(page).toHaveURL(/q=dress/);
    await expect(page).toHaveURL(/sort=price-asc/);
  });
});

test.describe('layout and accessibility at seven viewports', () => {
  for (const width of VIEWPORTS) {
    test(`results and empty pages at ${width}px: no horizontal scroll, no axe violations`, async ({ page }) => {
      await page.setViewportSize({ width, height: 900 });
      for (const url of ['/search?q=dress', '/search?q=qzxjvkw', '/search']) {
        await page.goto(url);
        const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
        expect(overflow, `${url} at ${width}`).toBeLessThanOrEqual(0);
        const results = await new AxeBuilder({ page }).withTags(AXE_TAGS).analyze();
        expect(results.violations.map((v) => `${v.id}: ${v.nodes[0]?.target}`), `${url} at ${width}`).toEqual([]);
      }
    });
  }
});
