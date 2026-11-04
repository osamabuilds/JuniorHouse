import AxeBuilder from '@axe-core/playwright';
import { Page, expect, test } from '@playwright/test';
import { openHydrated, registerCustomer } from './customer-session';

/**
 * The wishlist journey in the storefront container (WSL-1 to WSL-3, WSL-9): a guest taps a heart and is asked to sign in with the product
 * remembered in the browser; after registering, the product is on the account wishlist exactly once; removing it empties the list.
 * Run: `docker compose -f docker-compose.yml -f docker-compose.secure.yml up -d --build`, then
 * `STOREFRONT_BASE_URL=http://localhost:4000 npx playwright test e2e/customers/wishlist`. It needs one published product.
 */
const BASE = process.env['STOREFRONT_BASE_URL'] ?? 'http://localhost:4000';
const AXE_TAGS = ['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'];

test.use({ baseURL: BASE });

/** The first product page found through the New in listing, and the name shown for it. */
async function firstProduct(page: Page): Promise<{ path: string; name: string }> {
  await page.goto('/new-in');
  const links = await page.locator('main article a[href]').evaluateAll((all) =>
    all.map((a) => ({ href: (a as HTMLAnchorElement).getAttribute('href') ?? '', text: (a.textContent ?? '').trim() })),
  );
  const product = links.find((l) => /^\/[a-z0-9-]+\/[a-z0-9-]+\/[a-z0-9-]+$/.test(l.href) && l.text.length > 0);
  if (!product) throw new Error('No product found on /new-in: publish one first.');
  return { path: product.href, name: product.text };
}

test.describe('wishlist', () => {
  test('SEO-05 the wishlist page is not cached or indexed, and the wishlist API refuses a stranger', async ({ request }) => {
    const response = await request.get('/wishlist');
    expect(response.status()).toBe(200);
    expect(response.headers()['cache-control']).toContain('no-store');
    expect(await response.text()).toContain('noindex');

    expect((await request.get('/customer-api/wishlist/ids')).status()).toBe(401);
    expect((await request.get('/customer-api/wishlist/not-a-route')).status()).toBe(404);
  });

  test('WSL-1 to WSL-3, WSL-9 a guest saves a product, signs up, and finds it on the account wishlist once', async ({ page }) => {
    const product = await firstProduct(page);

    // Signed out: the heart asks for a sign-in and the product is remembered in this browser.
    await openHydrated(page, product.path);
    const heart = page.getByRole('button', { name: /to wishlist$/ }).first();
    await expect(heart).toHaveAttribute('aria-pressed', 'false');
    await heart.click();
    const prompt = page.getByRole('dialog', { name: 'Keep your wishlist' });
    await expect(prompt).toBeVisible();
    expect(await page.evaluate(() => JSON.parse(localStorage.getItem('romp.wishlist.guest') ?? '[]').length)).toBe(1);
    await prompt.getByRole('button', { name: 'Not now' }).click();
    await expect(prompt).toBeHidden();
    await expect(page.getByRole('button', { name: /from wishlist$/ }).first()).toHaveAttribute('aria-pressed', 'true');

    // Registering merges it into the account and clears the browser copy.
    await registerCustomer(page);
    await openHydrated(page, '/wishlist');
    await expect(page.getByRole('heading', { level: 1, name: 'Wishlist' })).toBeVisible();
    const cards = page.locator('ul.rp-grid > li > article');
    await expect(cards).toHaveCount(1);
    await expect(cards.first().getByRole('heading', { level: 2 })).toContainText(product.name);
    expect(await page.evaluate(() => localStorage.getItem('romp.wishlist.guest'))).toBeNull();

    const axe = await new AxeBuilder({ page }).withTags(AXE_TAGS).analyze();
    expect(axe.violations.map((v) => v.id)).toEqual([]);

    // The heart on the product page shows it saved; pressing the heart on the wishlist page takes it off.
    await openHydrated(page, product.path);
    await expect(page.getByRole('button', { name: /from wishlist$/ }).first()).toHaveAttribute('aria-pressed', 'true');
    await openHydrated(page, '/wishlist');
    await cards.first().getByRole('button', { name: /from wishlist$/ }).click();
    await expect(cards).toHaveCount(0);
    await expect(page.getByText('Nothing saved yet')).toBeVisible();
  });
});
