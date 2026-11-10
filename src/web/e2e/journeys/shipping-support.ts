import { createHmac } from 'node:crypto';
import { execSync } from 'node:child_process';
import { Page, expect } from '@playwright/test';

/**
 * Shared steps for the shipping journeys (S15 ship-track, S16 COD reconciliation): a shopper places and confirms a cash on delivery order, staff pick and
 * pack it by scanning, the booking worker gives it a waybill, and the fake courier posts signed status pushes to the real webhook.
 */
export const STOREFRONT = process.env['STOREFRONT_BASE_URL'] ?? 'http://localhost:4000';
export const ADMIN = process.env['ADMIN_BASE_URL'] ?? 'http://localhost:4201';
export const API = process.env['API_BASE_URL'] ?? 'http://localhost:8080';
const PRIMARY_SECRET = process.env['E2E_PRIMARY_COURIER_SECRET'] ?? 'local-demo-courier-primary-secret-0123456789';

export function sql(statement: string): string {
  const user = process.env['POSTGRES_USER'] ?? 'romp';
  const db = process.env['POSTGRES_DB'] ?? 'romp';
  return execSync(`docker compose exec -T postgres psql -U ${user} -d ${db} -tA -c "${statement.replace(/"/g, '\\"')}"`, { stdio: 'pipe' }).toString().trim();
}

export async function openHydrated(page: Page, path: string): Promise<void> {
  await page.goto(path);
  await page.waitForLoadState('networkidle');
  await page.waitForFunction(() => document.querySelector('[ngh]') === null);
}

/** Puts units of the SKU on the shelf of bin 1, straight into the database with a ledger row (staff would use putaway). */
function stockUp(sku: string, units: number): void {
  const variant = `(SELECT "ID" FROM "WHSE"."VRNT_REF" WHERE "SKU_CODE" = '${sku}')`;
  const before = `COALESCE((SELECT "QTY" FROM "WHSE"."STCK_BAL" WHERE "VRNT_REF_ID" = ${variant} AND "GRD_ID" = 1 AND "BIN_ID" = 1), 0)`;
  sql(
    `INSERT INTO "WHSE"."STCK_MVMT" ("VRNT_REF_ID","GRD_ID","BIN_ID","MVMT_KIND_ID","REF_ID","QTY_CHNG","BEF_QTY","AFT_QTY","ACTR_NAME","INSR_DTE","INSR_BY") SELECT ${variant},1,1,2,1,${units},${before},${before}+${units},'e2e',now(),'e2e'; ` +
      `INSERT INTO "WHSE"."STCK_BAL" ("VRNT_REF_ID","GRD_ID","BIN_ID","QTY","INSR_DTE","INSR_BY") SELECT ${variant},1,1,${units},now(),'e2e' ON CONFLICT ("VRNT_REF_ID","GRD_ID","BIN_ID") DO UPDATE SET "QTY" = "STCK_BAL"."QTY" + ${units}; ` +
      `UPDATE "WHSE"."VRNT_REF" SET "STCK_VER" = "STCK_VER" + 1 WHERE "SKU_CODE" = '${sku}'`,
  );
}

/** A guest adds the first listed product, checks out with cash on delivery and confirms. Returns the order number. */
export async function placeConfirmedCodOrder(page: Page): Promise<string> {
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

  await openHydrated(page, '/checkout');
  await page.getByLabel('Your name').fill('E2E Shopper');
  await page.getByLabel('Mobile number').fill('0300 1234567');
  await page.getByLabel('Full name').fill('E2E Shopper');
  await page.getByLabel('Phone for the courier').fill('0300 1234567');
  await page.getByLabel('Address', { exact: true }).fill('House 4, Street 2');
  await page.getByLabel('Province').selectOption({ index: 1 });
  await expect(page.getByLabel('City')).toBeEnabled();
  await page.getByLabel('City').selectOption({ index: 1 });
  await page.getByRole('button', { name: 'Place order' }).click();
  await expect(page).toHaveURL(/\/order-placed\/RMP-\d{4}-\d{5}-\d$/);
  const number = page.url().split('/').pop()!;
  await page.getByRole('button', { name: 'Confirm my order' }).click();
  await expect(page.getByText('your order is confirmed')).toBeVisible();
  return number;
}

async function scan(page: Page, inputId: string, code: string): Promise<void> {
  const box = page.locator(`#${inputId}`);
  await expect(box).toBeFocused();
  await page.keyboard.type(code);
  await page.keyboard.press('Enter');
}

/** Staff (already signed in) pick the order by scanning its bin and item, then pack it with the expected weight and seal it. */
export async function pickAndPack(page: Page, number: string): Promise<void> {
  await expect.poll(() => sql(`SELECT "ID" FROM "FULF"."FULF_ORDR" WHERE "ORDR_NO" = '${number}'`), { timeout: 60_000 }).not.toBe('');
  const id = Number(sql(`SELECT "ID" FROM "FULF"."FULF_ORDR" WHERE "ORDR_NO" = '${number}'`));
  const bin = sql(`SELECT a."BIN_CODE" FROM "FULF"."FULF_ALOC" a JOIN "FULF"."FULF_LINE" l ON l."ID" = a."LINE_ID" WHERE l."ORDR_ID" = ${id} LIMIT 1`);
  const gtin = sql(`SELECT "GTIN" FROM "FULF"."FULF_LINE" WHERE "ORDR_ID" = ${id} LIMIT 1`);
  const units = Number(sql(`SELECT SUM("QTY") FROM "FULF"."FULF_LINE" WHERE "ORDR_ID" = ${id}`));

  await page.goto('/fulfilment/pick');
  await page.getByRole('row', { name: new RegExp(number) }).getByRole('button', { name: `Pick order ${number}` }).click();
  await expect(page.getByRole('heading', { level: 1, name: `Pick order ${number}` })).toBeVisible();
  await scan(page, 'ps-scan-input', bin);
  for (let i = 0; i < units; i++) {
    await scan(page, 'ps-scan-input', gtin);
    await expect(page.locator('.scan-banner')).toContainText('Accepted');
  }
  await page.getByRole('button', { name: 'Complete the pick' }).click();
  await expect(page.getByText('The pick is complete')).toBeVisible();

  await page.goto('/fulfilment/pack');
  await page.getByRole('row', { name: new RegExp(number) }).getByRole('button', { name: `Pack order ${number}` }).click();
  await expect(page.getByRole('heading', { level: 1, name: `Pack order ${number}` })).toBeVisible();
  const grams = Number(/about (\d+) g/.exec((await page.getByText(/should weigh about/).textContent()) ?? '')?.[1]);
  for (let i = 0; i < units; i++) {
    await scan(page, 'pk-scan-input', gtin);
    await expect(page.locator('.scan-banner')).toContainText('Accepted');
  }
  await page.getByLabel('Weight in grams').fill(String(grams));
  await page.getByRole('button', { name: 'Save the weight' }).click();
  await page.getByRole('button', { name: 'Seal the parcel' }).click();
  await expect(page.getByText('The parcel is sealed')).toBeVisible();
}

/** The waybill the booking worker gave the parcel (it runs about once a minute). */
export async function waitForWaybill(number: string): Promise<string> {
  await expect.poll(() => sql(`SELECT COALESCE("WYBL_NO",'') FROM "SHPG"."SHPG_MAIN" WHERE "ORDR_NO" = '${number}'`), { timeout: 150_000, intervals: [3000] }).not.toBe('');
  return sql(`SELECT "WYBL_NO" FROM "SHPG"."SHPG_MAIN" WHERE "ORDR_NO" = '${number}'`);
}

/** The courier posts a signed status push to the real webhook, as a courier would. */
export async function courierPush(page: Page, waybill: string, status: string): Promise<void> {
  const body = JSON.stringify({ waybill, status, eventId: `e2e-${status}-${Date.now()}`, at: new Date().toISOString() });
  const signature = createHmac('sha256', PRIMARY_SECRET).update(body).digest('hex');
  const response = await page.request.post(`${API}/api/shipping/webhooks/PRIMARY`, {
    data: body,
    headers: { 'content-type': 'application/json', 'X-Courier-Signature': signature },
  });
  expect(response.ok()).toBeTruthy();
}
