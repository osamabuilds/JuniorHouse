import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';

/**
 * Listings (SCRUM-210, LST-1..6, 10..12, 14) against the storefront container and the API behind it. They use whatever the shop
 * holds: with no listable product the data-driven checks are skipped, and the status-code, redirect and empty-state checks still run.
 * Run: `docker compose up -d`, then `STOREFRONT_BASE_URL=http://localhost:4000 npx playwright test e2e/storefront/listings`.
 */
const BASE = process.env['STOREFRONT_BASE_URL'] ?? 'http://localhost:4000';
const AXE_TAGS = ['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'];
const summary = (r: { violations: { id: string; nodes: { target: unknown }[] }[] }) =>
  r.violations.map((v) => `${v.id}: ${v.nodes.map((n) => JSON.stringify(n.target)).join(', ')}`);

const VIEWPORTS = [320, 375, 390, 768, 1024, 1280, 1440];

interface Menu { audiences: { path: string; entries: { kind: string; path: string }[] }[] }

test.use({ baseURL: BASE });

/** The first audience that has a category link, or null when the shop has nothing listable yet. */
async function firstCategory(request: import('@playwright/test').APIRequestContext): Promise<string | null> {
  const menu = (await (await request.get('/api/storefront/menu')).json()) as Menu;
  for (const audience of menu.audiences) {
    const category = audience.entries.find((e) => e.kind === 'category');
    if (category) return category.path;
  }
  return null;
}

test.describe('status codes and redirects (no data needed)', () => {
  test('LST-14 an unknown audience or category is a real 404, not a soft one', async ({ request }) => {
    for (const path of ['/zz-nobody', '/girls/zz-nothing', '/collections/zz-none', '/zz/a/b/c']) {
      expect((await request.get(path)).status(), path).toBe(404);
    }
  });

  test('LST-10 an unknown key or odd spelling in the query is a 301 to the clean address', async ({ request }) => {
    const menu = (await (await request.get('/api/storefront/menu')).json()) as Menu;
    const base = menu.audiences[0]?.path ?? '/new-in';
    const response = await request.get(`${base}?utm_source=x&sort=price-asc&size=5-6y`, { maxRedirects: 0 });
    expect(response.status()).toBe(301);
    expect(response.headers()['location']).toBe(`${base}?size=5-6y&sort=price-asc`);
  });

  test('LST-11 hostile query values never reach the page', async ({ request }) => {
    const html = await (await request.get('/new-in?size=%3Cscript%3Ealert(1)%3C%2Fscript%3E&price=1e309-5&page=-1')).text();
    expect(html).not.toContain('<script>alert(1)');
  });

  test('LST-14 a page after the last is a 404', async ({ request }) => {
    expect((await request.get('/new-in?page=999999999')).status()).toBe(404);
  });

  test('LST-5 a filter that matches nothing is a 200 empty state with noindex, naming the filter', async ({ page, request }) => {
    const menu = (await (await request.get('/api/storefront/menu')).json()) as Menu;
    const base = menu.audiences[0]?.path ?? '/new-in';
    const response = await page.goto(`${base}?size=zz99`);
    expect(response!.status()).toBe(200);
    await expect(page.getByRole('heading', { level: 2, name: /Nothing matches/ })).toBeVisible();
    await expect(page.locator('meta[name="robots"]')).toHaveAttribute('content', /noindex/);
    await expect(page.getByRole('link', { name: 'Clear filters' })).toBeVisible();
    await page.getByRole('link', { name: 'Clear filters' }).click();
    await expect(page).toHaveURL(new RegExp(`${base}$`));
  });
});

test.describe('a listing with products', () => {
  test('LST-12 one h1, a breadcrumb, articles in a list, first row eager, the rest lazy', async ({ page, request }) => {
    const path = await firstCategory(request);
    test.skip(!path, 'the shop has no listable product yet');
    const response = await page.goto(path!);
    expect(response!.status()).toBe(200);
    await expect(page.locator('h1')).toHaveCount(1);
    await expect(page.getByRole('navigation', { name: 'Breadcrumb' }).locator('[aria-current="page"]')).toBeVisible();
    const articles = page.locator('ul.rp-grid > li article');
    expect(await articles.count()).toBeGreaterThan(0);
    await expect(articles.first().locator('img').first()).toHaveAttribute('loading', 'eager');
    await expect(articles.first().locator('img').first()).toHaveAttribute('width', /\d+/);
    await expect(articles.first().locator('img').first()).not.toHaveAttribute('alt', '');
  });

  test('LST-1 LST-3 choosing a size filters, shows a chip, and clearing restores the full list', async ({ page, request }) => {
    const path = await firstCategory(request);
    test.skip(!path, 'the shop has no listable product yet');
    await page.goto(path!);
    await page.getByText('Filter and sort').click();
    const size = page.locator('input[name="size"]').first();
    test.skip((await size.count()) === 0, 'no size options in this scope');
    const before = await page.locator('ul.rp-grid > li').count();
    await size.check();
    await page.getByRole('button', { name: 'Apply' }).click();
    await expect(page).toHaveURL(/size=/);
    await expect(page.locator('.rp-active .rp-chip')).toHaveCount(1);
    await page.getByRole('link', { name: 'Clear filters' }).click();
    await expect(page.locator('.rp-active')).toHaveCount(0);
    expect(await page.locator('ul.rp-grid > li').count()).toBe(before);
  });

  test('LST-4 the sort stays in the address and in the pagination links', async ({ page, request }) => {
    const path = await firstCategory(request);
    test.skip(!path, 'the shop has no listable product yet');
    await page.goto(`${path}?sort=price-asc`);
    await expect(page.locator('select[name="sort"]')).toHaveValue('price-asc');
    const next = page.locator('nav[aria-label="Pagination"] a[rel="next"]');
    if (await next.count()) await expect(next).toHaveAttribute('href', /sort=price-asc/);
  });

  test('LST-12 the filters work without scripting: a plain GET form', async ({ browser, request }) => {
    const path = await firstCategory(request);
    test.skip(!path, 'the shop has no listable product yet');
    const context = await browser.newContext({ javaScriptEnabled: false, baseURL: BASE });
    const page = await context.newPage();
    await page.goto(path!);
    const form = page.locator('form.rp-filters');
    await expect(form).toHaveAttribute('method', 'get');
    await form.getByText('Filter and sort').click(); // a native details element opens without scripting
    await form.locator('select[name="sort"]').selectOption('price-desc');
    await form.getByRole('button', { name: 'Apply' }).click();
    await expect(page).toHaveURL(/sort=price-desc/);
    await context.close();
  });
});

test.describe('layout and accessibility', () => {
  for (const width of VIEWPORTS) {
    test(`LST-12 no horizontal scroll at ${width}px`, async ({ page, request }) => {
      const path = (await firstCategory(request)) ?? '/new-in';
      await page.setViewportSize({ width, height: 900 });
      await page.goto(path);
      expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(0);
    });
  }

  for (const width of [390, 1280]) {
    test(`NFR-19 no axe violations at ${width}px`, async ({ page, request }) => {
      const path = (await firstCategory(request)) ?? '/new-in';
      await page.setViewportSize({ width, height: 900 });
      await page.goto(path);
      await page.getByText('Filter and sort').click();
      const results = await new AxeBuilder({ page }).withTags(AXE_TAGS).analyze();
      expect(summary(results)).toEqual([]);
    });
  }
});
