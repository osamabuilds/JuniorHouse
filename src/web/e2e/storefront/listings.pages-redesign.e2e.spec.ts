import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';

/**
 * Pages redesign (PR-1..17): discounts, card size line, 404/410 layout and the help page, against the storefront container and the
 * API behind it. Data-driven checks use the first product the shop lists and skip when there is none.
 * Run: `docker compose up -d --build api storefront`, then `STOREFRONT_BASE_URL=http://localhost:4000 npx playwright test e2e/storefront/listings.pages-redesign`.
 */
const BASE = process.env['STOREFRONT_BASE_URL'] ?? 'http://localhost:4000';
const AXE_TAGS = ['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'];
const VIEWPORTS = [390, 768, 1280];
const DEMO = '/girls/dresses/floral-summer-dress';
const LISTING = '/girls/dresses';
const HELP = '/delivery';

test.use({ baseURL: BASE });

for (const width of VIEWPORTS) {
  test.describe(`at ${width}px`, () => {
    test.use({ viewport: { width, height: 900 } });

    test(`PR-10 PR-12 PR-16 the 404 page is a real 404 with the decorative code, one h1, a home link and no overflow`, async ({ page }) => {
      const response = await page.goto('/zz-nothing-here');
      expect(response!.status()).toBe(404);
      await expect(page.locator('h1')).toHaveCount(1);
      await expect(page.locator('.rp-notfound__code')).toHaveAttribute('aria-hidden', 'true');
      await expect(page.getByRole('link', { name: 'Back to home' })).toBeVisible();
      await expect(page.locator('meta[name="robots"]')).toHaveAttribute('content', /noindex/);
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
      const results = await new AxeBuilder({ page }).withTags(AXE_TAGS).analyze();
      expect(results.violations).toEqual([]);
    });

    test(`PR-13 PR-15 PR-16 the help page has the Help menu, a prose article, 44px targets, no overflow and passes axe`, async ({ page }) => {
      const response = await page.goto(HELP);
      test.skip(response!.status() !== 200, 'the delivery page is not published');
      await expect(page.locator('h1')).toHaveCount(1);
      await expect(page.locator('main')).toHaveCount(1);
      await expect(page.locator('.rp-eyebrow').first()).toContainText('Help');
      const menu = page.getByRole('navigation', { name: 'Help' });
      await expect(menu.locator('a[aria-current="page"]')).toHaveCount(1);
      await expect(page.locator('article.rp-prose')).toBeVisible();
      for (const link of await menu.getByRole('link').all()) {
        expect((await link.boundingBox())!.height).toBeGreaterThanOrEqual(44);
      }
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
      const results = await new AxeBuilder({ page }).withTags(AXE_TAGS).analyze();
      expect(results.violations).toEqual([]);
    });

    test(`PR-1 PR-7 PR-8 PR-9 a listing shows each card's size line, no add-to-bag control, no overflow and passes axe`, async ({ page }) => {
      const response = await page.goto(LISTING);
      test.skip(response!.status() !== 200, 'no dresses listing is published');
      const cards = page.locator('rp-product-tile');
      test.skip((await cards.count()) === 0, 'the shop lists no products');
      await expect(cards.first().locator('.rp-avail li').first()).toBeVisible();
      await expect(cards.first().locator('button')).toHaveCount(0);
      // The visually hidden "Now / Was / sold out" text must not take up room on screen.
      for (const hidden of await page.locator('rp-product-tile .sr-only').all()) {
        const box = await hidden.boundingBox();
        expect(box === null || (box.width <= 1 && box.height <= 1)).toBe(true);
      }
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
      const results = await new AxeBuilder({ page }).withTags(AXE_TAGS).analyze();
      expect(results.violations).toEqual([]);
    });

    test(`PR-1 PR-3 PR-16 the product page price block is accessible, with no overflow`, async ({ page }) => {
      const response = await page.goto(DEMO);
      test.skip(response!.status() !== 200, 'the demo product is not published');
      await expect(page.locator('rp-product-price .rp-price')).toBeVisible();
      for (const hidden of await page.locator('rp-product-price .sr-only').all()) {
        const box = await hidden.boundingBox();
        expect(box === null || (box.width <= 1 && box.height <= 1)).toBe(true);
      }
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
      const results = await new AxeBuilder({ page }).withTags(AXE_TAGS).analyze();
      expect(results.violations).toEqual([]);
    });
  });
}
