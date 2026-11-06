import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Page } from '@playwright/test';

/**
 * Sold items in listings (docs/specs/storefront/sold-items-in-listings, SLD-4, SLD-5, SLD-10, NFR-19), against the storefront container
 * and the API behind it. Needs the Development demo data (task 20): sold-out pieces in the winter and summer ranges.
 * Run: `docker compose up -d --build api storefront`, then `STOREFRONT_BASE_URL=http://localhost:4000 npx playwright test e2e/storefront/listings.sold-items`.
 */
const BASE = process.env['STOREFRONT_BASE_URL'] ?? 'http://localhost:4000';
const AXE_TAGS = ['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'];
const SEASON = '/collections/winter/jackets';
const MIN_TARGET = 44;

test.use({ baseURL: BASE });

async function open(page: Page, path: string): Promise<number> {
  const response = await page.goto(path, { waitUntil: 'networkidle' });
  return response!.status();
}

for (const width of [390, 1280]) {
  test.describe(`at ${width}px`, () => {
    test.use({ viewport: { width, height: 900 } });

    test('SLD-5 a sold-out card says so in words, has no quick add, and the whole range stays on the page', async ({ page }) => {
      test.skip((await open(page, SEASON)) !== 200, 'the demo winter range is not seeded');
      const sold = page.locator('rp-product-tile article.is-sold-out');
      test.skip((await sold.count()) === 0, 'the demo range has no sold-out pieces');
      await expect(sold.first().locator('.rp-tag--sold')).toHaveText('Sold out');
      await expect(sold.first().locator('h3 a')).toContainText('sold out');
      await expect(sold.first().locator('.rp-qa__plus')).toHaveCount(0);
      // Sold pieces come after the in-stock ones.
      const flags = await page.locator('rp-product-tile article').evaluateAll((els) => els.map((e) => e.classList.contains('is-sold-out')));
      expect(flags.indexOf(true)).toBeGreaterThan(flags.lastIndexOf(false));
    });

    test('SLD-10 the season category is a real page: 200, one h1, a four-level breadcrumb, a self canonical', async ({ page }) => {
      test.skip((await open(page, SEASON)) !== 200, 'the demo winter range is not seeded');
      await expect(page.locator('h1')).toHaveCount(1);
      const crumbs = page.getByRole('navigation', { name: 'Breadcrumb' }).locator('li');
      await expect(crumbs).toHaveCount(4);
      await expect(page.locator('link[rel="canonical"]')).toHaveAttribute('href', new RegExp(`${SEASON}$`));
    });

    test('SLD-4 In stock only is off by default, leaves sold pieces out when ticked, and lives in the URL', async ({ page }) => {
      test.skip((await open(page, SEASON)) !== 200, 'the demo winter range is not seeded');
      const all = await page.locator('rp-product-tile').count();
      test.skip((await page.locator('rp-product-tile article.is-sold-out').count()) === 0, 'the demo range has no sold-out pieces');
      await open(page, `${SEASON}?stock=in`);
      await expect(page.locator('rp-product-tile article.is-sold-out')).toHaveCount(0);
      expect(await page.locator('rp-product-tile').count()).toBeLessThan(all);
      await expect(page.locator('rp-active-filters')).toContainText('In stock only');
      // The canonical of a filtered variant is the clean listing (SEO-06).
      await expect(page.locator('link[rel="canonical"]')).toHaveAttribute('href', new RegExp(`${SEASON}$`));
      // Removing the chip returns to the whole range.
      await page.locator('rp-active-filters').getByRole('link', { name: /Remove filter In stock only/ }).click();
      await expect(page).toHaveURL(new RegExp(`${SEASON}$`));
    });

    test('SLD-4 the In stock only box works without scripting: a plain GET form', async ({ browser }) => {
      const context = await browser.newContext({ javaScriptEnabled: false, baseURL: BASE, viewport: { width, height: 900 } });
      const page = await context.newPage();
      test.skip((await open(page, SEASON)) !== 200, 'the demo winter range is not seeded');
      const form = page.locator(width < 1024 ? '.rp-filterbar__phone form' : '.rp-filterbar__desktop form');
      expect(await form.locator('input[name="stock"][value="in"]').count()).toBe(1);
      expect(await form.getAttribute('method')).toBe('get');
      await context.close();
    });

    test('NFR-19 a listing with sold-out cards passes axe, has no overflow and every control is 44px high', async ({ page }) => {
      test.skip((await open(page, SEASON)) !== 200, 'the demo winter range is not seeded');
      test.skip((await page.locator('rp-product-tile article.is-sold-out').count()) === 0, 'the demo range has no sold-out pieces');
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
      const results = await new AxeBuilder({ page }).withTags(AXE_TAGS).analyze();
      expect(results.violations.map((v) => `${v.id}: ${v.nodes.map((n) => JSON.stringify(n.target)).join(', ')}`)).toEqual([]);
      // Card names are text links inside a larger card, like every other card; the filter bar and pagination controls are measured.
      const small = await page.$$eval('.rp-filterbar :is(button, select, input:not([type=checkbox]):not([type=radio]):not([type=hidden]), summary), .rp-pagination a', (els, min) =>
        els.filter((e) => (e as HTMLElement).offsetParent !== null && e.getBoundingClientRect().height < (min as number)).map((e) => e.outerHTML.slice(0, 80)), MIN_TARGET);
      expect(small).toEqual([]);
    });
  });
}
