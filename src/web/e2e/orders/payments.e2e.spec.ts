import { execSync } from 'node:child_process';
import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Page } from '@playwright/test';

/**
 * Online payment in the storefront container against the pretend gateway (orders payments Y1 to Y9, PAY-1, PAY-2, PAY-3, PAY-10): a guest pays
 * by card through the demo payment page, a decline can be retried, a payment whose callback is lost is never shown as paid on the browser coming
 * back, and pressing Pay twice never opens a second payment. It needs one published product, and the local stack started with the pretend
 * gateway on (docker-compose.secure.yml turns it on). Run:
 * `docker compose -f docker-compose.yml -f docker-compose.secure.yml up -d --build`, then
 * `STOREFRONT_BASE_URL=http://localhost:4000 npx playwright test e2e/orders/payments`.
 */
const BASE = process.env['STOREFRONT_BASE_URL'] ?? 'http://localhost:4000';
const AXE_TAGS = ['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'];

test.use({ baseURL: BASE });

function sql(statement: string): string {
  const user = process.env['POSTGRES_USER'] ?? 'romp';
  const db = process.env['POSTGRES_DB'] ?? 'romp';
  return execSync(`docker compose exec -T postgres psql -U ${user} -d ${db} -tA -c "${statement.replace(/"/g, '\\"')}"`, { stdio: 'pipe' }).toString().trim();
}

async function openHydrated(page: Page, path: string): Promise<void> {
  await page.goto(path);
  await page.waitForLoadState('networkidle');
  await page.waitForFunction(() => document.querySelector('[ngh]') === null);
}

/**
 * Puts a few units of every variant on a shelf, straight into the local database with ledger rows (the local data has products with no stock,
 * and staff would use putaway in the admin, which needs a login). A size with no stock cannot be added to the bag, so this runs first.
 */
function stockEverything(units: number): void {
  const before = `(SELECT COALESCE(SUM(b."QTY"), 0) FROM "WHSE"."STCK_BAL" b WHERE b."VRNT_REF_ID" = v."ID" AND b."GRD_ID" = 1 AND b."BIN_ID" = 1)`;
  sql(
    `INSERT INTO "WHSE"."STCK_MVMT" ("VRNT_REF_ID","GRD_ID","BIN_ID","MVMT_KIND_ID","REF_ID","QTY_CHNG","BEF_QTY","AFT_QTY","ACTR_NAME","INSR_DTE","INSR_BY") SELECT v."ID",1,1,2,1,${units},${before},${before}+${units},'e2e',now(),'e2e' FROM "WHSE"."VRNT_REF" v; ` +
      `INSERT INTO "WHSE"."STCK_BAL" ("VRNT_REF_ID","GRD_ID","BIN_ID","QTY","INSR_DTE","INSR_BY") SELECT v."ID",1,1,${units},now(),'e2e' FROM "WHSE"."VRNT_REF" v ON CONFLICT ("VRNT_REF_ID","GRD_ID","BIN_ID") DO UPDATE SET "QTY" = "STCK_BAL"."QTY" + ${units}; ` +
      `UPDATE "WHSE"."VRNT_REF" SET "STCK_VER" = "STCK_VER" + 1`,
  );
}

/** The first listed product's page, with its first size chosen and the page ready to use. */
async function openFirstProduct(page: Page): Promise<void> {
  await page.goto('/new-in');
  const hrefs = await page.locator('main a[href]').evaluateAll((links) => links.map((a) => (a as HTMLAnchorElement).getAttribute('href') ?? ''));
  const product = hrefs.find((h) => /^\/[a-z0-9-]+\/[a-z0-9-]+\/[a-z0-9-]+$/.test(h));
  if (!product) throw new Error('No product found on /new-in: publish one first.');
  await openHydrated(page, product);
  await page.locator('rp-size-options a').first().click();
  await page.waitForLoadState('networkidle');
  await page.waitForFunction(() => document.querySelector('[ngh]') === null);
}

/** Adds the first listed product to the bag. The size must already show as available (see the setup below). */
async function addFirstProduct(page: Page): Promise<void> {
  await openFirstProduct(page);
  await page.getByRole('button', { name: 'Add to bag' }).click();
  await expect(page.getByRole('button', { name: 'Bag, 1 item' })).toBeVisible();
}

/** Fills the checkout, chooses Card and presses Place order: the shopper lands on the pretend payment page. */
async function placeCardOrder(page: Page): Promise<{ number: string; token: string }> {
  await addFirstProduct(page);
  await openHydrated(page, '/checkout');
  await page.getByLabel('Your name').fill('E2E Payer');
  await page.getByLabel('Mobile number').fill('0300 1112223');
  await page.getByLabel('Full name').fill('E2E Payer');
  await page.getByLabel('Phone for the courier').fill('0300 1112223');
  await page.getByLabel('Address', { exact: true }).fill('House 7, Street 5');
  await page.getByLabel('Province').selectOption({ index: 1 });
  await expect(page.getByLabel('City')).toBeEnabled();
  await page.getByLabel('City').selectOption({ index: 1 });
  await page.getByRole('radio', { name: /Card/ }).check();
  const placed = page.waitForResponse((r) => r.url().endsWith('/customer-api/orders') && r.request().method() === 'POST');
  await page.getByRole('button', { name: 'Place order' }).click();
  const body = (await (await placed).json()) as { orderNumber: string; payToken: string | null };
  expect(body.payToken).toBeTruthy();
  await page.waitForURL(/\/customer-api\/orders\/payments\/fake\/fk_[0-9a-f]{24}$/, { timeout: 30_000 });
  expect(body.orderNumber).toMatch(/^RMP-\d{4}-\d{5}-\d$/);
  return { number: body.orderNumber, token: body.payToken! };
}

const orderStatus = (number: string): string => sql(`SELECT "ORDR_STS_ID" FROM "ORDR"."ORDR_MAIN" WHERE "ORDR_NO" = '${number}'`);
const paymentStatus = (number: string): string =>
  sql(`SELECT p."PAYM_STS_ID" FROM "ORDR"."PAYM_MAIN" p JOIN "ORDR"."ORDR_MAIN" o ON o."ID" = p."ORDR_ID" WHERE o."ORDR_NO" = '${number}'`);
const attempts = (number: string): string =>
  sql(`SELECT count(*) FROM "ORDR"."PAYM_ATMP" a JOIN "ORDR"."PAYM_MAIN" p ON p."ID" = a."PAYM_ID" JOIN "ORDR"."ORDR_MAIN" o ON o."ID" = p."ORDR_ID" WHERE o."ORDR_NO" = '${number}'`);
const paidEvents = (number: string): string =>
  sql(`SELECT count(*) FROM "ORDR"."OUTB_MSG" WHERE "EVNT_TYP" = 'OrderPaidEvent' AND "PYLD"::text LIKE '%${number}%'`);

test.describe('online payment', () => {
  test.setTimeout(120_000);

  // A size with no stock cannot be added to the bag, and the shop's stock view follows the warehouse after a while (minutes, not seconds):
  // put stock on the shelf once, then wait once until the first size of the first product can be added.
  test.beforeAll(async ({ browser }) => {
    test.setTimeout(540_000);
    stockEverything(20);
    const context = await browser.newContext({ baseURL: BASE });
    const page = await context.newPage();
    const add = page.getByRole('button', { name: 'Add to bag' });
    await openFirstProduct(page);
    const deadline = Date.now() + 480_000;
    while (await add.isDisabled()) {
      if (Date.now() > deadline) throw new Error('The shop never showed the stocked size as available: is the read model running?');
      await page.waitForTimeout(10_000);
      await page.reload();
      await page.waitForLoadState('networkidle');
      await page.waitForFunction(() => document.querySelector('[ngh]') === null);
    }

    await context.close();
  });

  test('PAY-10 the payment pages are not cached or indexed', async ({ request }) => {
    for (const path of ['/checkout/pay?token=x', '/checkout/payment-return?token=x']) {
      const response = await request.get(path);
      expect(response.status()).toBe(200);
      expect(response.headers()['cache-control']).toContain('no-store');
      expect(await response.text()).toContain('noindex');
    }
  });

  test('PAY-1, TC-CHK-11 a guest pays by card on the demo page and the order ends Paid, once', async ({ page }) => {
    const { number } = await placeCardOrder(page);
    expect(orderStatus(number)).toBe('3');
    expect(paymentStatus(number)).toBe('2');
    expect(attempts(number)).toBe('1');

    await page.getByRole('button', { name: 'Pay now' }).click();

    await expect(page).toHaveURL(/\/checkout\/payment-return\?token=/);
    await expect(page.getByRole('heading', { level: 1, name: 'Payment received' })).toBeVisible({ timeout: 30_000 });
    await expect(page.getByText(`Order ${number}`)).toBeVisible();
    const axe = await new AxeBuilder({ page }).withTags(AXE_TAGS).analyze();
    expect(axe.violations.map((v) => v.id)).toEqual([]);

    expect(orderStatus(number)).toBe('4');
    expect(paymentStatus(number)).toBe('3');
    expect(paidEvents(number)).toBe('1');

    // Reloading the return page, or pressing Back, never charges again and still says the same.
    await page.reload();
    await expect(page.getByRole('heading', { level: 1, name: 'Payment received' })).toBeVisible({ timeout: 30_000 });
    expect(attempts(number)).toBe('1');
    expect(paidEvents(number)).toBe('1');
  });

  test('PAY-2 a declined card can be retried and then succeeds; the order keeps its stock meanwhile', async ({ page }) => {
    const { number } = await placeCardOrder(page);

    await page.getByRole('button', { name: 'Decline the card' }).click();

    await expect(page.getByRole('heading', { level: 1, name: 'Your payment did not go through' })).toBeVisible({ timeout: 30_000 });
    await expect(page.getByText('Nothing was taken')).toBeVisible();
    expect(orderStatus(number)).toBe('3');
    expect(paymentStatus(number)).toBe('4');

    await page.getByRole('button', { name: 'Try paying again' }).click();
    await page.waitForURL(/\/customer-api\/orders\/payments\/fake\/fk_[0-9a-f]{24}$/, { timeout: 30_000 });
    expect(attempts(number)).toBe('2');
    await page.getByRole('button', { name: 'Pay now' }).click();

    await expect(page.getByRole('heading', { level: 1, name: 'Payment received' })).toBeVisible({ timeout: 30_000 });
    expect(orderStatus(number)).toBe('4');
    expect(paidEvents(number)).toBe('1');
  });

  test('PAY-10, PAY-6 when the callback is lost the browser coming back is never shown as paid', async ({ page }) => {
    const { number } = await placeCardOrder(page);

    await page.getByRole('button', { name: 'Pay, but lose the callback' }).click();

    await expect(page).toHaveURL(/\/checkout\/payment-return\?token=/);
    await expect(page.getByRole('heading', { level: 1, name: 'Confirming your payment' })).toBeVisible({ timeout: 30_000 });
    await expect(page.getByText('Please do not press Back or pay again')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Payment received' })).toHaveCount(0);
    expect(orderStatus(number)).toBe('3');
    expect(paymentStatus(number)).toBe('2');
    expect(paidEvents(number)).toBe('0');
  });

  test('PAY-3 pressing Pay a second time opens the same payment, never a second one', async ({ page }) => {
    const { number, token } = await placeCardOrder(page);
    const firstPage = page.url();

    // Opening the pay page again for the same order hands back the page that is already open.
    await page.goto(`/checkout/pay?token=${encodeURIComponent(token)}`);
    await page.waitForURL(/\/customer-api\/orders\/payments\/fake\/fk_[0-9a-f]{24}$/, { timeout: 30_000 });

    expect(page.url()).toBe(firstPage);
    expect(attempts(number)).toBe('1');
  });

  test('SEC-05 a tampered or unknown payment link says it could not be found and shows nothing about any order', async ({ page }) => {
    await openHydrated(page, '/checkout/payment-return?token=not-a-real-token');
    await expect(page.getByRole('heading', { level: 1, name: 'We could not find that payment' })).toBeVisible({ timeout: 30_000 });
    await expect(page.getByText(/RMP-\d{4}/)).toHaveCount(0);

    await openHydrated(page, '/checkout/pay?token=%27%20OR%201%3D1%20--');
    await expect(page.getByRole('heading', { level: 1 })).toContainText(/could not (open|find)/i, { timeout: 30_000 });

    await openHydrated(page, '/checkout/payment-return');
    await expect(page.getByRole('heading', { level: 1, name: 'We could not find that payment' })).toBeVisible();
  });
});
