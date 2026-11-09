import { execSync } from 'node:child_process';
import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';

/**
 * The promo code box on the bag (cart D8, FR-25, FR-26, DSC-1, DSC-4, DSC-9): a guest adds a product, types a code, sees the discount and the
 * new total, removes it, and sees a plain reason for a code that is not valid. The code is written straight into the local database with
 * psql (staff would create it in the admin, which needs a login), so this runs against the docker stack only.
 * Run: `docker compose -f docker-compose.yml -f docker-compose.secure.yml up -d --build`, then
 * `STOREFRONT_BASE_URL=http://localhost:4000 npx playwright test e2e/cart/discount-codes`. It needs one published product with a size that can be bought.
 */
const BASE = process.env['STOREFRONT_BASE_URL'] ?? 'http://localhost:4000';
const AXE_TAGS = ['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'];

test.use({ baseURL: BASE });

function sql(statement: string): void {
  const user = process.env['POSTGRES_USER'] ?? 'romp';
  const db = process.env['POSTGRES_DB'] ?? 'romp';
  execSync(`docker compose exec -T postgres psql -U ${user} -d ${db} -c "${statement.replace(/"/g, '\\"')}"`, { stdio: 'pipe' });
}

async function openHydrated(page: import('@playwright/test').Page, path: string): Promise<void> {
  await page.goto(path);
  await page.waitForLoadState('networkidle');
  await page.waitForFunction(() => document.querySelector('[ngh]') === null);
}

async function addFirstProduct(page: import('@playwright/test').Page): Promise<void> {
  await page.goto('/new-in');
  const hrefs = await page.locator('main a[href]').evaluateAll((links) => links.map((a) => (a as HTMLAnchorElement).getAttribute('href') ?? ''));
  const product = hrefs.find((h) => /^\/[a-z0-9-]+\/[a-z0-9-]+\/[a-z0-9-]+$/.test(h));
  if (!product) throw new Error('No product found on /new-in: publish one first.');
  await openHydrated(page, product);
  await page.locator('rp-size-options a').first().click();
  await page.waitForLoadState('networkidle');
  await page.getByRole('button', { name: 'Add to bag' }).click();
  await expect(page.getByRole('button', { name: 'Bag, 1 item' })).toBeVisible();
}

test.describe('discount codes in the bag', () => {
  const code = `E2E${Date.now().toString(36).toUpperCase()}`;

  test.beforeAll(() => {
    sql(
      `INSERT INTO "CART"."DISC_CODE" ("CODE","DISC_TYPE_ID","VAL","SALE_INCL_IND","ACT_IND","INSR_DTE","INSR_BY") VALUES ('${code}',1,10,true,true,now(),'e2e')`,
    );
  });

  test.afterAll(() => {
    sql(`UPDATE "CART"."CART_MAIN" SET "DISC_CODE_ID" = NULL WHERE "DISC_CODE_ID" IN (SELECT "ID" FROM "CART"."DISC_CODE" WHERE "CODE" = '${code}')`);
    sql(`DELETE FROM "CART"."DISC_CODE" WHERE "CODE" = '${code}'`);
  });

  test('FR-25, FR-26, DSC-4, DSC-9 a code is applied only on Apply, shows its discount, can be removed, and a bad code gets a plain reason', async ({ page }) => {
    await addFirstProduct(page);
    await openHydrated(page, '/cart');
    await expect(page.getByRole('heading', { level: 1, name: 'Your bag' })).toBeVisible();

    // Nothing is applied until the shopper presses Apply, and an unknown code is refused in words.
    const box = page.getByLabel('Promo code');
    await expect(box).toBeVisible();
    await box.fill('NOSUCHCODE');
    await page.getByRole('button', { name: 'Apply' }).click();
    await expect(page.getByText('That code is not valid.')).toBeVisible();
    await expect(page.locator('.rp-promo-chip')).toHaveCount(0);

    // A real code, typed in lower case, takes money off and shows on its own row.
    await box.fill(code.toLowerCase());
    await page.getByRole('button', { name: 'Apply' }).click();
    await expect(page.locator('.rp-promo-chip')).toContainText(code);
    await expect(page.locator('.rp-sumrow--sale')).toContainText(`Discount (${code})`);

    // It survives a reload, and the page stays accessible.
    await page.reload();
    await page.waitForLoadState('networkidle');
    await expect(page.locator('.rp-promo-chip')).toContainText(code);
    const axe = await new AxeBuilder({ page }).withTags(AXE_TAGS).analyze();
    expect(axe.violations.map((v) => v.id)).toEqual([]);

    // Removing it puts the box back and takes the row away.
    await page.getByRole('button', { name: `Remove code ${code}` }).click();
    await expect(page.getByLabel('Promo code')).toBeVisible();
    await expect(page.locator('.rp-sumrow--sale')).toHaveCount(0);
  });
});
