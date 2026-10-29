import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';
import { uniqueSuffix } from '../support';

/**
 * Address book journey (ADD-1, ADD-2, ADD-8) in the storefront container, against the API's stand-in customer (Development only).
 * Run: `docker compose up -d`, then `STOREFRONT_BASE_URL=http://localhost:4000 npx playwright test e2e/customers/addresses`.
 */
const BASE = process.env['STOREFRONT_BASE_URL'] ?? 'http://localhost:4000';
const AXE_TAGS = ['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'];

test.use({ baseURL: BASE });

test.describe('addresses', () => {
  test('SEO-05 the page is not cached or indexed', async ({ request }) => {
    const response = await request.get('/account/addresses');
    expect(response.status()).toBe(200);
    expect(response.headers()['cache-control']).toContain('no-store');
    expect(await response.text()).toContain('noindex, nofollow');
  });

  test('ADD-1, ADD-2, ADD-8 a customer adds two addresses, switches the default and removes one', async ({ page }) => {
    const mark = uniqueSuffix();
    const first = `E2E Home ${mark}`;
    const second = `E2E Office ${mark}`;

    async function add(name: string): Promise<void> {
      await page.getByRole('button', { name: 'Add an address' }).click();
      const dialog = page.locator('dialog[data-form]');
      await dialog.getByLabel('Recipient name').fill(name);
      await dialog.getByLabel('Phone number').fill('0300 1234567');
      await dialog.getByLabel('Province').selectOption({ index: 1 });
      await expect(dialog.getByLabel('City')).toBeEnabled();
      await dialog.getByLabel('City').selectOption({ index: 1 });
      await dialog.getByLabel('Address line 1').fill('House 12, Street 4');
      await dialog.getByRole('button', { name: 'Save address' }).click();
      await expect(dialog).toBeHidden();
    }

    await page.goto('/account');
    await expect(page).toHaveURL(/\/account\/addresses$/);
    await expect(page.getByRole('heading', { level: 1, name: 'Addresses' })).toBeVisible();

    await add(first);
    await add(second);

    const card = (name: string) => page.locator('ul.rp-address-list > li').filter({ hasText: name });
    await expect(card(first)).toBeVisible();
    await expect(card(second)).toBeVisible();

    await card(second).getByRole('button', { name: /Make default/ }).click();
    await expect(card(second)).toContainText('Default');
    await expect(card(first)).not.toContainText('Default');

    const axe = await new AxeBuilder({ page }).withTags(AXE_TAGS).analyze();
    expect(axe.violations.map((v) => v.id)).toEqual([]);

    for (const name of [first, second]) {
      await card(name).getByRole('button', { name: /Remove/ }).click();
      await page.locator('dialog[data-confirm] button[data-confirm-remove]').click();
      await expect(card(name)).toHaveCount(0);
    }
  });
});
