import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';

/**
 * The basket journey in the storefront container (cart C10, CRT-1 to CRT-4): a guest picks a size, adds to the bag, sees the drawer, opens the
 * full bag, changes a quantity, removes the line and sees the empty state. The basket lives on the API behind the storefront's pass-through,
 * with the `romp-guest` cookie on this origin.
 * Run: `docker compose -f docker-compose.yml -f docker-compose.secure.yml up -d --build`, then
 * `STOREFRONT_BASE_URL=http://localhost:4000 npx playwright test e2e/cart`. It needs one published product with a size that can be bought.
 */
const BASE = process.env['STOREFRONT_BASE_URL'] ?? 'http://localhost:4000';
const AXE_TAGS = ['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'];

test.use({ baseURL: BASE });

async function openHydrated(page: import('@playwright/test').Page, path: string): Promise<void> {
  await page.goto(path);
  await page.waitForLoadState('networkidle');
  await page.waitForFunction(() => document.querySelector('[ngh]') === null);
}

/** The first product page found through the New in listing. */
async function firstProductPath(page: import('@playwright/test').Page): Promise<string> {
  await page.goto('/new-in');
  const hrefs = await page.locator('main a[href]').evaluateAll((links) => links.map((a) => (a as HTMLAnchorElement).getAttribute('href') ?? ''));
  const product = hrefs.find((h) => /^\/[a-z0-9-]+\/[a-z0-9-]+\/[a-z0-9-]+$/.test(h));
  if (!product) throw new Error('No product found on /new-in: publish one first.');
  return product;
}

test.describe('cart', () => {
  test('SEO-05 the bag page is not cached or indexed, and the basket API never caches', async ({ request }) => {
    const page = await request.get('/cart');
    expect(page.status()).toBe(200);
    expect(page.headers()['cache-control']).toContain('no-store');
    expect(await page.text()).toContain('noindex');

    const api = await request.get('/customer-api/cart');
    expect(api.status()).toBe(200);
    expect(api.headers()['cache-control']).toContain('no-store');
  });

  test('CRT-2 an empty bag says so, with one clear next step', async ({ page }) => {
    await openHydrated(page, '/cart');

    await expect(page.getByRole('heading', { level: 1, name: 'Your bag is empty' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Start shopping' })).toBeVisible();
    const axe = await new AxeBuilder({ page }).withTags(AXE_TAGS).analyze();
    expect(axe.violations.map((v) => v.id)).toEqual([]);
  });

  test('CRT-1, CRT-2, CRT-4 a guest adds a size, changes the quantity and removes it', async ({ page }) => {
    const path = await firstProductPath(page);
    await openHydrated(page, path);

    // Add to bag waits for a size, and says why in words.
    const add = page.getByRole('button', { name: 'Add to bag' });
    await expect(add).toBeDisabled();
    await expect(page.getByText('Choose a size to add this to your bag.')).toBeVisible();

    const size = page.locator('rp-size-options a').first();
    await size.click();
    await page.waitForLoadState('networkidle');
    await expect(add).toBeEnabled();
    await add.click();

    // The drawer opens with the line, the subtotal and a link to the full bag.
    const drawer = page.getByRole('dialog', { name: 'Your bag' });
    await expect(drawer).toBeVisible();
    await expect(drawer.getByRole('heading', { name: /Your bag \(1\)/ })).toBeVisible();
    await expect(drawer.getByText(/Subtotal/)).toBeVisible();
    await expect(page.getByRole('button', { name: 'Bag, 1 item' })).toBeVisible();
    const axeDrawer = await new AxeBuilder({ page }).withTags(AXE_TAGS).analyze();
    expect(axeDrawer.violations.map((v) => v.id)).toEqual([]);

    await drawer.getByRole('link', { name: 'View bag' }).click();
    await expect(page).toHaveURL(/\/cart$/);
    await expect(page.getByRole('heading', { level: 1, name: 'Your bag' })).toBeVisible();
    await expect(page.locator('.rp-bag-lines > li')).toHaveCount(1);

    // A quantity change updates the count and the figures without a reload.
    await page.getByRole('button', { name: 'Increase quantity' }).click();
    await expect(page.getByRole('button', { name: 'Bag, 2 items' })).toBeVisible();
    await expect(page.locator('.rp-count-note')).toContainText('2 items');

    // The basket survives a reload (the guest cookie and the server-side basket).
    await page.reload();
    await page.waitForLoadState('networkidle');
    await expect(page.locator('.rp-count-note')).toContainText('2 items');

    const axe = await new AxeBuilder({ page }).withTags(AXE_TAGS).analyze();
    expect(axe.violations.map((v) => v.id)).toEqual([]);

    // Minus at one removes the line (the quantity is 2 here, so press minus twice).
    await page.getByRole('button', { name: 'Decrease quantity' }).click();
    await expect(page.locator('.rp-count-note')).toContainText('1 item');
    await page.getByRole('button', { name: 'Remove from bag' }).click();
    await expect(page.getByRole('heading', { level: 1, name: 'Your bag is empty' })).toBeVisible();
  });

  test('CRT-9 another browser never sees this basket', async ({ page, browser }) => {
    const path = await firstProductPath(page);
    await openHydrated(page, path);
    await page.locator('rp-size-options a').first().click();
    await page.waitForLoadState('networkidle');
    await page.getByRole('button', { name: 'Add to bag' }).click();
    await expect(page.getByRole('button', { name: 'Bag, 1 item' })).toBeVisible();

    const other = await browser.newContext({ baseURL: BASE });
    const otherPage = await other.newPage();
    await openHydrated(otherPage, '/cart');
    await expect(otherPage.getByRole('heading', { level: 1, name: 'Your bag is empty' })).toBeVisible();
    await other.close();

    // Leave nothing behind.
    await openHydrated(page, '/cart');
    await page.locator('.rp-line__remove').click();
  });
});
