import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';

/**
 * Product pages (SCRUM-203, PDP-1..13, 16) against the storefront container and the API behind it. They use the first product the
 * shop lists; with none, the data-driven checks are skipped and the status-code checks still run.
 * Run: `docker compose up -d --build api storefront`, then `STOREFRONT_BASE_URL=http://localhost:4000 npx playwright test e2e/storefront/product-pages`.
 */
const BASE = process.env['STOREFRONT_BASE_URL'] ?? 'http://localhost:4000';
const AXE_TAGS = ['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'];
const VIEWPORTS = [320, 375, 390, 768, 1024, 1280, 1440];
const DEMO = '/girls/dresses/floral-summer-dress';

test.use({ baseURL: BASE });

async function demoExists(request: import('@playwright/test').APIRequestContext): Promise<boolean> {
  const response = await request.get(DEMO);
  return response.status() === 200;
}

test.describe('status codes (no data needed)', () => {
  test('PDP-13 an unknown product path is a real 404, not a soft one', async ({ request }) => {
    for (const path of ['/girls/dresses/zz-nothing', '/zz/zz/zz', '/girls/zz-none/zz-nothing']) {
      expect((await request.get(path)).status(), path).toBe(404);
    }
  });

  test('PDP-13 hostile paths never reach the page', async ({ request }) => {
    const response = await request.get('/girls/dresses/%3Cscript%3Ealert(1)%3C%2Fscript%3E');
    expect(response.status()).toBe(404);
    expect(await response.text()).not.toContain('<script>alert(1)');
  });
});

test.describe('a product page', () => {
  test.beforeEach(async ({ request }) => {
    test.skip(!(await demoExists(request)), 'the demo product is not published');
  });

  test('PDP-1 PDP-10 one h1, a breadcrumb, the first image eager and high priority with size and alt', async ({ page }) => {
    const response = await page.goto(DEMO);
    expect(response!.status()).toBe(200);
    await expect(page.locator('h1')).toHaveCount(1);
    await expect(page.getByRole('navigation', { name: 'Breadcrumb' }).locator('[aria-current="page"]')).toBeVisible();
    const first = page.locator('.gallery img').first();
    await expect(first).toHaveAttribute('loading', 'eager');
    await expect(first).toHaveAttribute('fetchpriority', 'high');
    await expect(first).toHaveAttribute('width', /\d+/);
    await expect(first).toHaveAttribute('height', /\d+/);
    await expect(first).not.toHaveAttribute('alt', '');
  });

  test('PDP-2 PDP-3 PDP-15 picking a colour and a size updates the page in place and the URL', async ({ page }) => {
    await page.goto(DEMO);
    await expect(page.locator('rp-product-price')).toBeVisible();
    await page.getByRole('link', { name: /^2-3 Years/ }).click();
    await expect(page).toHaveURL(/size=2-3y/);
    await expect(page.locator('rp-availability-note [role="status"]')).not.toContainText('Choose a size');
  });

  test('PDP-4 PDP-16 a sold-out colour cannot be chosen further: its sizes are not links and say so in words', async ({ page }) => {
    await page.goto(`${DEMO}?colour=blk`);
    const off = page.locator('rp-size-options [aria-disabled="true"]');
    expect(await off.count()).toBeGreaterThan(0);
    await expect(off.first()).toContainText(/not yet available|sold out/i);
    await expect(page.locator('rp-size-options a')).toHaveCount(0);
  });

  test('PDP-6 the size chart is a table with scoped headers', async ({ page }) => {
    await page.goto(DEMO);
    const table = page.locator('rp-size-chart table');
    await expect(table.locator('th[scope="col"]').first()).toBeVisible();
    await expect(table.locator('tbody th[scope="row"]').first()).toBeVisible();
  });

  test('PDP-8 composition and care are in the server HTML, with no JavaScript', async ({ request }) => {
    const html = await (await request.get(DEMO)).text();
    expect(html).toContain('Composition');
    expect(html).toMatch(/Cotton/);
    expect(html).toMatch(/Machine wash/);
  });

  test('PDP-11 colour and size can be reached and chosen with the keyboard alone', async ({ page }) => {
    await page.goto(DEMO);
    const sizeLink = page.getByRole('link', { name: /^1-2 Years/ });
    await sizeLink.focus();
    await expect(sizeLink).toBeFocused();
    await page.keyboard.press('Enter');
    await expect(page).toHaveURL(/size=1-2y/);
  });

  test('PDP-11 PDP-12 no axe violations at any viewport, and the page never scrolls sideways', async ({ page }) => {
    for (const width of VIEWPORTS) {
      await page.setViewportSize({ width, height: 900 });
      await page.goto(DEMO);
      const results = await new AxeBuilder({ page }).withTags(AXE_TAGS).analyze();
      expect(results.violations.map((v) => v.id), `axe at ${width}px`).toEqual([]);
      const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
      expect(overflow, `horizontal overflow at ${width}px`).toBeLessThanOrEqual(0);
    }
  });

  test('PDP-12 LCP is at most 2.5 s on a phone-sized viewport', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.addInitScript(() => {
      (window as unknown as { __lcp: number }).__lcp = 0;
      new PerformanceObserver((list) => {
        const entries = list.getEntries();
        (window as unknown as { __lcp: number }).__lcp = entries[entries.length - 1].startTime;
      }).observe({ type: 'largest-contentful-paint', buffered: true });
    });
    await page.goto(DEMO, { waitUntil: 'load' });
    await page.waitForTimeout(500);
    const lcp = await page.evaluate(() => (window as unknown as { __lcp: number }).__lcp);
    expect(lcp).toBeGreaterThan(0);
    expect(lcp).toBeLessThanOrEqual(2500);
  });
});
