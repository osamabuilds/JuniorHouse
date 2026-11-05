import { execSync } from 'node:child_process';
import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';

/**
 * Track your order in the storefront container (orders L5, OLK-1 to OLK-9): a guest places a cash on delivery order, finds it with the
 * number and the mobile, sees the same words for every mismatch, gets the Turnstile check (development token) after repeated misses, and
 * the page is noindex, uncached and passes axe. It needs one published product with a size that can be bought.
 * Run: `docker compose -f docker-compose.yml -f docker-compose.secure.yml up -d --build`, then
 * `STOREFRONT_BASE_URL=http://localhost:4000 npx playwright test e2e/orders/order-lookup`.
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

function stockUp(sku: string, units: number): void {
  const variant = `(SELECT "ID" FROM "WHSE"."VRNT_REF" WHERE "SKU_CODE" = '${sku}')`;
  const before = `COALESCE((SELECT "QTY" FROM "WHSE"."STCK_BAL" WHERE "VRNT_REF_ID" = ${variant} AND "GRD_ID" = 1 AND "BIN_ID" = 1), 0)`;
  sql(
    `INSERT INTO "WHSE"."STCK_MVMT" ("VRNT_REF_ID","GRD_ID","BIN_ID","MVMT_KIND_ID","REF_ID","QTY_CHNG","BEF_QTY","AFT_QTY","ACTR_NAME","INSR_DTE","INSR_BY") SELECT ${variant},1,1,2,1,${units},${before},${before}+${units},'e2e',now(),'e2e'; ` +
      `INSERT INTO "WHSE"."STCK_BAL" ("VRNT_REF_ID","GRD_ID","BIN_ID","QTY","INSR_DTE","INSR_BY") SELECT ${variant},1,1,${units},now(),'e2e' ON CONFLICT ("VRNT_REF_ID","GRD_ID","BIN_ID") DO UPDATE SET "QTY" = "STCK_BAL"."QTY" + ${units}; ` +
      `UPDATE "WHSE"."VRNT_REF" SET "STCK_VER" = "STCK_VER" + 1 WHERE "SKU_CODE" = '${sku}'`,
  );
}

/** Adds the first listed product to the bag through the UI, stocks it, and places a cash on delivery order through the API. */
async function placeOrder(page: import('@playwright/test').Page, mobile: string): Promise<string> {
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

  const provinces = await (await page.request.get('/api/geography/provinces')).json();
  const cities = await (await page.request.get(`/api/geography/provinces/${provinces[0].id}/cities`)).json();
  const response = await page.request.post('/customer-api/orders', {
    headers: { Origin: BASE, 'Idempotency-Key': `e2e-${Date.now()}-${Math.floor(Math.random() * 1e6)}` },
    data: {
      contact: { name: 'Ayesha Khan', email: 'ayesha@example.com', mobile, whatsAppOptIn: false },
      address: { recipientName: 'Ayesha Khan', recipientPhone: mobile, lineOne: 'House 4, Street 2', provinceId: provinces[0].id, cityId: cities[0].id },
      paymentMethod: 'COD',
    },
  });
  expect(response.status()).toBe(201);
  return (await response.json()).orderNumber as string;
}

test.describe('track your order', () => {
  // The throttle is per address and every test here shares one, so each test starts with a clean slate.
  test.beforeEach(() => {
    sql('DELETE FROM "ORDR"."LOOK_ATMP"');
  });

  test('OLK-9 the page is not cached or indexed, and has one h1', async ({ request, page }) => {
    const response = await request.get('/track-order');
    expect(response.status()).toBe(200);
    expect(response.headers()['cache-control']).toContain('no-store');
    expect(await response.text()).toContain('noindex');

    await openHydrated(page, '/track-order');
    await expect(page.getByRole('heading', { level: 1, name: 'Track your order' })).toHaveCount(1);
  });

  test('OLK-9 the form passes axe, with and without an error', async ({ page }) => {
    await openHydrated(page, '/track-order');
    expect((await new AxeBuilder({ page }).withTags(AXE_TAGS).analyze()).violations).toEqual([]);

    await page.getByLabel('Order number').fill('RMP-2026-99999-0');
    await page.getByLabel('Mobile number').fill('03001234567');
    await page.getByRole('button', { name: 'Find my order' }).click();
    await expect(page.getByRole('alert')).toContainText('Those details do not match an order');
    expect((await new AxeBuilder({ page }).withTags(AXE_TAGS).analyze()).violations).toEqual([]);
  });


  test('OLK-1 OLK-6 OLK-7 OLK-2 a guest finds the order, sees it masked, and a wrong mobile reads like a wrong number', async ({ page }) => {
    const mobile = `0300${Math.floor(1_000_000 + Math.random() * 8_999_999)}`;
    const number = await placeOrder(page, mobile);

    await openHydrated(page, '/track-order');
    await find(page, number.toLowerCase(), `+92 ${mobile.slice(1, 4)} ${mobile.slice(4)}`);

    await expect(page.getByText(`Order ${number}`)).toBeVisible();
    await expect(page.getByText('03XX-XXX')).toBeVisible();
    const text = await page.locator('main').innerText();
    expect(text).not.toContain('House 4');
    expect(text).not.toContain(mobile.slice(1));
    expect((await new AxeBuilder({ page }).withTags(AXE_TAGS).analyze()).violations).toEqual([]);

    await page.getByRole('button', { name: 'Look up another order' }).click();
    await find(page, number, '03009999999');
    const wrongMobile = ((await page.getByRole('alert').textContent()) ?? '').replace(/\s+/g, ' ').trim();
    await find(page, 'RMP-2026-99999-0', mobile);
    await expect(page.getByRole('alert')).toHaveText(wrongMobile);
  });

  test('OLK-4 after repeated misses the Turnstile check appears, and the development token lets the next try through', async ({ page }) => {
    const mobile = `0300${Math.floor(1_000_000 + Math.random() * 8_999_999)}`;
    const number = await placeOrder(page, mobile);
    await openHydrated(page, '/track-order');

    for (let i = 0; i < 4; i++) {
      await fill(page, number, `0300111111${i}`);
      await page.getByRole('button', { name: 'Find my order' }).click();
      await expect(page.getByRole('alert')).toBeVisible();
    }
    await expect(page.getByRole('alert')).toContainText('One more step');
    await expect(page.locator('rp-turnstile')).toBeAttached();

    await fill(page, number, mobile);
    await page.getByRole('button', { name: 'Find my order' }).click();
    await expect(page.getByText(`Order ${number}`)).toBeVisible();
  });
});

async function fill(page: import('@playwright/test').Page, number: string, mobile: string): Promise<void> {
  await page.getByLabel('Order number').fill(number);
  await page.getByLabel('Mobile number').fill(mobile);
}

/**
 * Finds an order the way a person would. All the tests share one address, so an earlier miss may already have switched the check on: when
 * the page says so, press once more (the development token is passed on by the page).
 */
async function find(page: import('@playwright/test').Page, number: string, mobile: string): Promise<void> {
  await fill(page, number, mobile);
  await page.getByRole('button', { name: 'Find my order' }).click();
  const check = page.getByRole('alert').filter({ hasText: 'One more step' });
  if (await check.isVisible({ timeout: 2000 }).catch(() => false)) {
    await page.getByRole('button', { name: 'Find my order' }).click();
  }
}
