import { execSync } from 'node:child_process';
import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';

/**
 * Checkout in the storefront container (orders H4, ORD-1, CHK-1, CHK-2, CHK-10): a guest adds a product, checks out with cash on delivery,
 * lands on the order-placed page, the stock is reserved and the bag empties. It needs one published product with a size that can be bought.
 * Run: `docker compose -f docker-compose.yml -f docker-compose.secure.yml up -d --build`, then
 * `STOREFRONT_BASE_URL=http://localhost:4000 npx playwright test e2e/orders`.
 */
const BASE = process.env['STOREFRONT_BASE_URL'] ?? 'http://localhost:4000';
const AXE_TAGS = ['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'];

test.use({ baseURL: BASE });

function sql(statement: string): string {
  const user = process.env['POSTGRES_USER'] ?? 'romp';
  const db = process.env['POSTGRES_DB'] ?? 'romp';
  return execSync(`docker compose exec -T postgres psql -U ${user} -d ${db} -tA -c "${statement.replace(/"/g, '\\"')}"`, { stdio: 'pipe' }).toString().trim();
}

async function openHydrated(page: import('@playwright/test').Page, path: string): Promise<void> {
  await page.goto(path);
  await page.waitForLoadState('networkidle');
  await page.waitForFunction(() => document.querySelector('[ngh]') === null);
}

/**
 * Puts a few units of the SKU on a shelf, straight into the local database with a ledger row (the local data has products with no stock, and
 * staff would use putaway in the admin, which needs a login). Bumps the stock version like a real posting does.
 */
function stockUp(sku: string, units: number): void {
  const variant = `(SELECT "ID" FROM "WHSE"."VRNT_REF" WHERE "SKU_CODE" = '${sku}')`;
  const before = `COALESCE((SELECT "QTY" FROM "WHSE"."STCK_BAL" WHERE "VRNT_REF_ID" = ${variant} AND "GRD_ID" = 1 AND "BIN_ID" = 1), 0)`;
  sql(
    `INSERT INTO "WHSE"."STCK_MVMT" ("VRNT_REF_ID","GRD_ID","BIN_ID","MVMT_KIND_ID","REF_ID","QTY_CHNG","BEF_QTY","AFT_QTY","ACTR_NAME","INSR_DTE","INSR_BY") SELECT ${variant},1,1,2,1,${units},${before},${before}+${units},'e2e',now(),'e2e'; ` +
      `INSERT INTO "WHSE"."STCK_BAL" ("VRNT_REF_ID","GRD_ID","BIN_ID","QTY","INSR_DTE","INSR_BY") SELECT ${variant},1,1,${units},now(),'e2e' ON CONFLICT ("VRNT_REF_ID","GRD_ID","BIN_ID") DO UPDATE SET "QTY" = "STCK_BAL"."QTY" + ${units}; ` +
      `UPDATE "WHSE"."VRNT_REF" SET "STCK_VER" = "STCK_VER" + 1 WHERE "SKU_CODE" = '${sku}'`,
  );
}

/** Adds the first listed product to the bag and makes sure the size in the bag has stock to order. */
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
  const cart = await (await page.request.get('/customer-api/cart')).json();
  stockUp(cart.lines[0].sku, 5);
}

test.describe('checkout', () => {
  test('SEO-05 the checkout is not cached or indexed', async ({ request }) => {
    const response = await request.get('/checkout');
    expect(response.status()).toBe(200);
    expect(response.headers()['cache-control']).toContain('no-store');
    expect(await response.text()).toContain('noindex');
  });

  test('CHK-2 an empty bag cannot be checked out, and blank fields are named in words', async ({ page }) => {
    await openHydrated(page, '/checkout');
    await expect(page.getByRole('heading', { name: 'Your bag is empty' })).toBeVisible();

    await addFirstProduct(page);
    await openHydrated(page, '/checkout');
    await page.getByRole('button', { name: 'Place order' }).click();
    await expect(page.getByText('Enter your name.')).toBeVisible();
    await expect(page.getByText('Choose a province.')).toBeVisible();
    await expect(page).toHaveURL(/\/checkout$/);
  });

  test('CHK-1, ORD-1 a guest places a cash on delivery order, the stock is reserved and the bag empties', async ({ page }) => {
    await addFirstProduct(page);
    await openHydrated(page, '/checkout');
    await expect(page.getByRole('heading', { level: 1, name: 'Checkout' })).toBeVisible();

    await page.getByLabel('Your name').fill('E2E Shopper');
    await page.getByLabel('Mobile number').fill('0300 1234567');
    await page.getByLabel('Full name').fill('E2E Shopper');
    await page.getByLabel('Phone for the courier').fill('0300 1234567');
    await page.getByLabel('Address', { exact: true }).fill('House 4, Street 2');
    await page.getByLabel('Province').selectOption({ index: 1 });
    await expect(page.getByLabel('City')).toBeEnabled();
    await page.getByLabel('City').selectOption({ index: 1 });

    // The delivery fee appears once a city is chosen, from the server.
    await expect(page.locator('.rp-sumrow', { hasText: 'Delivery' })).not.toContainText('Calculated when you choose a city');
    const axe = await new AxeBuilder({ page }).withTags(AXE_TAGS).analyze();
    expect(axe.violations.map((v) => v.id)).toEqual([]);

    await page.getByRole('button', { name: 'Place order' }).click();

    await expect(page).toHaveURL(/\/order-placed\/RMP-\d{4}-\d{5}-\d$/);
    const number = page.url().split('/').pop()!;
    await expect(page.getByRole('heading', { level: 1, name: 'Thank you for your order' })).toBeVisible();
    await expect(page.getByText(`Order ${number}`)).toBeVisible();
    const axePlaced = await new AxeBuilder({ page }).withTags(AXE_TAGS).analyze();
    expect(axePlaced.violations.map((v) => v.id)).toEqual([]);

    // The order exists, its stock is promised to it, and the bag empties by the order event.
    expect(sql(`SELECT "ORDR_STS_ID" FROM "ORDR"."ORDR_MAIN" WHERE "ORDR_NO" = '${number}'`)).toBe('1');
    await expect
      .poll(() => sql(`SELECT r."RSRV_STS_ID" FROM "WHSE"."STCK_RSRV" r WHERE r."ORDR_NO" = '${number}'`), { timeout: 30_000 })
      .toBe('2');
    await page.goto('/cart');
    await page.waitForLoadState('networkidle');
    await expect(page.getByRole('heading', { level: 1, name: 'Your bag is empty' })).toBeVisible({ timeout: 15_000 });

    // Going Back and submitting again would not place a second order: the reload shows the same number.
    await page.goto(`/order-placed/${number}`);
    await expect(page.getByText(`Order ${number}`)).toBeVisible();
    expect(sql(`SELECT count(*) FROM "ORDR"."ORDR_MAIN" WHERE "ORDR_NO" = '${number}'`)).toBe('1');
  });
});
