import { execSync } from 'node:child_process';
import AxeBuilder from '@axe-core/playwright';
import { Page, expect, test } from '@playwright/test';
import { OWNER_PASSWORD, signInAsOwner } from '../identity/support';

/**
 * Pick and pack, from a confirmed order to a sealed parcel (S14 A5; allocations, pick-lists, pick-exceptions, pack-station, packing-slips,
 * warehouse-app). A shopper places a cash on delivery order in the storefront and confirms it; the order enters the pick queue with its stock set
 * aside in a bin; staff pick it in the admin app by scanning (a wrong shelf and a wrong item are refused in words), pack it with a second scan and a
 * weight (a wrong weight needs a note), and print the slip with the cash to collect. A second order shows the exception path: the picker flags the
 * shelf, the line waits, staff drop it. Every screen is checked with axe.
 *
 * Needs the docker stack up (api, admin, storefront, postgres) and an Owner with the password in E2E_OWNER_PASSWORD (see the identity journeys).
 *   STOREFRONT_BASE_URL (default http://localhost:4000)   ADMIN_BASE_URL (default http://localhost:4201)
 * The database is reached through `docker compose exec postgres`, so set COMPOSE_PROJECT_NAME / COMPOSE_FILE when the stack is not the default one.
 */
const STOREFRONT = process.env['STOREFRONT_BASE_URL'] ?? 'http://localhost:4000';
const ADMIN = process.env['ADMIN_BASE_URL'] ?? 'http://localhost:4201';
const AXE_TAGS = ['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'];

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

/** A guest adds the first listed product, makes sure it has stock, checks out with cash on delivery and confirms with the button. Returns the order number. */
async function placeConfirmedCodOrder(page: Page): Promise<string> {
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

/** The fulfilment order the confirmation created, found by order number (the event is delivered by the outbox, so it may take a moment). */
async function waitForFulfilmentOrder(number: string): Promise<{ id: number; bin: string; gtin: string; units: number }> {
  await expect.poll(() => sql(`SELECT "ID" FROM "FULF"."FULF_ORDR" WHERE "ORDR_NO" = '${number}'`), { timeout: 60_000 }).not.toBe('');
  const id = Number(sql(`SELECT "ID" FROM "FULF"."FULF_ORDR" WHERE "ORDR_NO" = '${number}'`));
  const bin = sql(`SELECT a."BIN_CODE" FROM "FULF"."FULF_ALOC" a JOIN "FULF"."FULF_LINE" l ON l."ID" = a."LINE_ID" WHERE l."ORDR_ID" = ${id} LIMIT 1`);
  const gtin = sql(`SELECT "GTIN" FROM "FULF"."FULF_LINE" WHERE "ORDR_ID" = ${id} LIMIT 1`);
  const units = Number(sql(`SELECT SUM("QTY") FROM "FULF"."FULF_LINE" WHERE "ORDR_ID" = ${id}`));
  return { id, bin, gtin, units };
}

async function checkAccessible(page: Page): Promise<void> {
  const axe = await new AxeBuilder({ page }).withTags(AXE_TAGS).analyze();
  expect(axe.violations.map((v) => `${v.id}: ${v.nodes[0]?.html}`)).toEqual([]);
}

/** Types a code into the focused scan box and presses Enter, as a keyboard-wedge scanner does. */
async function scan(page: Page, inputId: string, code: string): Promise<void> {
  const box = page.locator(`#${inputId}`);
  await expect(box).toBeFocused();
  await page.keyboard.type(code);
  await page.keyboard.press('Enter');
}

test.describe('pick and pack', () => {
  test.skip(!OWNER_PASSWORD, 'set E2E_OWNER_PASSWORD to the Owner password created for this run');

  test('PCK-2 to PCK-7, PAK-1 to PAK-9, SLP-1, SLP-2, APP-3, APP-4: a cash on delivery order goes from confirmed to a sealed parcel with its slip', async ({ browser }) => {
    test.setTimeout(240_000);
    const shop = await browser.newPage({ baseURL: STOREFRONT });
    const number = await placeConfirmedCodOrder(shop);
    const order = await waitForFulfilmentOrder(number);
    const stockBefore = Number(sql(`SELECT SUM(b."QTY") FROM "WHSE"."STCK_BAL" b JOIN "WHSE"."VRNT_REF" v ON v."ID" = b."VRNT_REF_ID" WHERE v."GTIN" = '${order.gtin}'`));

    const staff = await browser.newContext({ baseURL: ADMIN });
    const page = await staff.newPage();
    await signInAsOwner(page);

    // The queue lists the order, oldest first, by number only; the picker takes it.
    await page.goto('/fulfilment/pick');
    await expect(page.getByRole('heading', { level: 1, name: 'Pick queue' })).toBeVisible();
    const row = page.getByRole('row', { name: new RegExp(number) });
    await expect(row).toBeVisible();
    await expect(row).not.toContainText('E2E Shopper');
    await checkAccessible(page);
    await row.getByRole('button', { name: `Pick order ${number}` }).click();

    // The station: the scan box has the focus and keeps it after every scan (APP-3).
    await expect(page.getByRole('heading', { level: 1, name: `Pick order ${number}` })).toBeVisible();
    await expect(page.locator('#ps-scan-input')).toBeFocused();
    await expect(page.getByRole('row', { name: new RegExp(order.bin) })).toBeVisible();

    // A wrong shelf, and then a wrong item, are refused in words and nothing is counted (PCK-3, PCK-4, APP-4).
    await scan(page, 'ps-scan-input', 'Z-99-9');
    await expect(page.locator('.scan-banner')).toContainText('Refused');
    await expect(page.locator('.scan-banner')).toContainText(`Go to ${order.bin}`);
    await scan(page, 'ps-scan-input', order.bin);
    await expect(page.locator('.scan-banner')).toContainText('Accepted');
    await scan(page, 'ps-scan-input', '2999999999990');
    await expect(page.locator('.scan-banner')).toContainText('Refused');
    await expect(page.locator('.scan-banner')).toContainText('not on the order');
    await expect(page.getByRole('button', { name: 'Complete the pick' })).toBeDisabled();
    await checkAccessible(page);

    // Every unit of the right item, then a unit too many is refused (PCK-5).
    for (let i = 0; i < order.units; i++) {
      await scan(page, 'ps-scan-input', order.gtin);
      await expect(page.locator('.scan-banner')).toContainText('Accepted');
    }
    await scan(page, 'ps-scan-input', order.gtin);
    await expect(page.locator('.scan-banner')).toContainText('already complete');

    await page.getByRole('button', { name: 'Complete the pick' }).click();
    await expect(page.getByText('The pick is complete')).toBeVisible();
    expect(sql(`SELECT "FULF_STS_ID" FROM "FULF"."FULF_ORDR" WHERE "ID" = ${order.id}`)).toBe('3');
    // The reserved stock became a real decrement, once (PCK-7, RSV-9).
    expect(Number(sql(`SELECT SUM(b."QTY") FROM "WHSE"."STCK_BAL" b JOIN "WHSE"."VRNT_REF" v ON v."ID" = b."VRNT_REF_ID" WHERE v."GTIN" = '${order.gtin}'`))).toBe(stockBefore - order.units);
    expect(sql(`SELECT r."RSRV_STS_ID" FROM "WHSE"."STCK_RSRV" r WHERE r."ORDR_NO" = '${number}'`)).toBe('3');

    // The pack station: the cash to collect is large; a second scan counts apart from the pick (PAK-1, PAK-11).
    await page.goto('/fulfilment/pack');
    await page.getByRole('row', { name: new RegExp(number) }).getByRole('button', { name: `Pack order ${number}` }).click();
    await expect(page.getByRole('heading', { level: 1, name: `Pack order ${number}` })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Cash to collect' })).toBeVisible();
    await expect(page.locator('.cash-due__amount')).toContainText('PKR');
    await expect(page.getByText(/Use a (Poly bag|Box)/)).toBeVisible();
    const expectedGrams = Number(/about (\d+) g/.exec((await page.getByText(/should weigh about/).textContent()) ?? '')?.[1]);
    expect(expectedGrams).toBeGreaterThan(0);
    for (let i = 0; i < order.units; i++) {
      await scan(page, 'pk-scan-input', order.gtin);
      await expect(page.locator('.scan-banner')).toContainText('Accepted');
    }
    await scan(page, 'pk-scan-input', order.gtin);
    await expect(page.locator('.scan-banner')).toContainText('Refused');
    await checkAccessible(page);

    // A weight that does not fit warns, and sealing needs a note (PAK-7); the right weight needs none (PAK-6).
    await page.getByLabel('Weight in grams').fill(String(expectedGrams * 4));
    await page.getByRole('button', { name: 'Save the weight' }).click();
    await expect(page.getByText('Check the parcel again')).toBeVisible();
    await expect(page.getByLabel(/The weight does not fit the order/)).toBeVisible();
    await expect(page.getByRole('button', { name: 'Seal the parcel' })).toBeDisabled();
    await page.getByLabel('Weight in grams').fill(String(expectedGrams));
    await page.getByRole('button', { name: 'Save the weight' }).click();
    await expect(page.getByLabel(/The weight does not fit the order/)).toHaveCount(0);
    await page.getByRole('button', { name: 'Seal the parcel' }).click();
    await expect(page.getByText('The parcel is sealed')).toBeVisible();
    expect(sql(`SELECT "FULF_STS_ID" FROM "FULF"."FULF_ORDR" WHERE "ID" = ${order.id}`)).toBe('5');

    // The slip: items, prices, the amount to collect, one heading, no phone number, and the printer port (SLP-1, SLP-2, SLP-7, SLP-8).
    await page.getByRole('link', { name: 'Print the packing slip' }).click();
    await expect(page.getByRole('heading', { level: 1, name: 'Packing slip' })).toBeVisible();
    await expect(page.locator('.slip')).toContainText(number);
    await expect(page.locator('.slip__pay--cod')).toContainText('Cash to collect');
    await expect(page.locator('.slip')).not.toContainText('0300');
    await expect(page.locator('.slip')).toContainText('E2E Shopper');
    await checkAccessible(page);
    await page.getByRole('button', { name: 'Send to the printer' }).click();
    await expect(page.getByText('The slip was sent to the printer')).toBeVisible();
    await page.getByRole('button', { name: 'Send to the printer' }).click();
    await expect(page.locator('.slip__reprint')).toContainText('Reprint');

    await shop.close();
    await staff.close();
  });

  test('PEX-1 to PEX-4, PEX-7, PEX-9: the picker flags a shelf that does not match, the line waits, and staff drop it', async ({ browser }) => {
    test.setTimeout(240_000);
    const shop = await browser.newPage({ baseURL: STOREFRONT });
    const number = await placeConfirmedCodOrder(shop);
    const order = await waitForFulfilmentOrder(number);

    const staff = await browser.newContext({ baseURL: ADMIN });
    const page = await staff.newPage();
    await signInAsOwner(page);
    await page.goto('/fulfilment/pick');
    await page.getByRole('row', { name: new RegExp(number) }).getByRole('button', { name: `Pick order ${number}` }).click();
    await expect(page.getByRole('heading', { level: 1, name: `Pick order ${number}` })).toBeVisible();

    // Nothing is forced: the picker says what the shelf holds (PEX-1, PEX-5).
    await page.getByRole('button', { name: 'The shelf does not match' }).click();
    await page.getByRole('button', { name: 'Flag it' }).click();
    await expect(page.getByText('Choose a reason')).toBeVisible();
    await page.getByLabel('What is wrong').selectOption('NOT_FOUND');
    await page.getByLabel('How many did you find on the shelf').fill('0');
    await checkAccessible(page);
    await page.getByRole('button', { name: 'Flag it' }).click();
    await expect(page.getByText('The line is flagged')).toBeVisible();
    await expect(page.getByText('Waiting for restock')).toBeVisible();
    expect(sql(`SELECT "FULF_STS_ID" FROM "FULF"."FULF_ORDR" WHERE "ID" = ${order.id}`)).toBe('6');

    // The customer is told through an event, and the bin is flagged for review (PEX-3, PEX-6).
    await expect.poll(() => sql(`SELECT count(*) FROM "WHSE"."BIN_MAIN" WHERE "BIN_CODE" = '${order.bin}' AND "RVW_RSN" IS NOT NULL`), { timeout: 60_000 }).toBe('1');

    // Staff see it in the queue with who and when, and drop the line (PEX-4, PEX-9).
    await page.goto('/fulfilment/exceptions');
    await expect(page.getByRole('heading', { level: 1, name: 'Pick exceptions' })).toBeVisible();
    const exception = page.getByRole('row', { name: new RegExp(number) });
    await expect(exception).toContainText(order.bin);
    await expect(exception).toContainText('Open');
    await checkAccessible(page);
    await exception.getByRole('button', { name: 'Drop the line' }).click();
    await expect(page.getByText(`Exception on ${number} resolved`)).toBeVisible();
    expect(sql(`SELECT "FULF_STS_ID" FROM "FULF"."FULF_ORDR" WHERE "ID" = ${order.id}`)).toBe('7');

    // The bin stays under review until staff count it: clear it so later runs can use it.
    sql(`UPDATE "WHSE"."BIN_MAIN" SET "RVW_RSN" = NULL WHERE "BIN_CODE" = '${order.bin}'`);
    await shop.close();
    await staff.close();
  });
});
