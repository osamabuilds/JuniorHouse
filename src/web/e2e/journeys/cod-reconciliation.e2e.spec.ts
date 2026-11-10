import AxeBuilder from '@axe-core/playwright';
import { Page, expect, test } from '@playwright/test';
import { OWNER_PASSWORD, signInAsOwner } from '../identity/support';
import { ADMIN, STOREFRONT, courierPush, pickAndPack, placeConfirmedCodOrder, sql, waitForWaybill } from './shipping-support';

/**
 * COD reconciliation (S16; docs/specs/shipping/cod-reconciliation): a delivered cash on delivery parcel waits for the courier's cash; finance records the
 * courier statement; a short payment is flagged and resolved with a note; recording the same statement again changes nothing.
 *
 * Needs the docker stack up and an Owner (see the identity journeys): E2E_OWNER_PASSWORD, STOREFRONT_BASE_URL, ADMIN_BASE_URL, API_BASE_URL.
 */
const AXE_TAGS = ['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'];

async function checkAccessible(page: Page): Promise<void> {
  const axe = await new AxeBuilder({ page }).withTags(AXE_TAGS).analyze();
  expect(axe.violations.map((v) => `${v.id}: ${v.nodes[0]?.html}`)).toEqual([]);
}

async function recordStatement(page: Page, reference: string, total: number, rows: string): Promise<void> {
  await page.goto('/dispatch/cod/remittances/new');
  await page.getByLabel('Statement reference').fill(reference);
  await page.getByLabel('Total on the statement (PKR)').fill(String(total));
  await page.getByLabel('Parcels (waybill, amount)').fill(rows);
  await page.getByRole('button', { name: 'Record the statement' }).click();
}

test.describe('COD reconciliation', () => {
  test.skip(!OWNER_PASSWORD, 'set E2E_OWNER_PASSWORD to the Owner password created for this run');

  test('COD-1 to COD-4, COD-8, COD-9, COD-12, COD-13: a delivered parcel is awaiting, a short payment is flagged and resolved, a repeat changes nothing', async ({ browser }) => {
    test.setTimeout(420_000);
    const shop = await browser.newPage({ baseURL: STOREFRONT });
    const number = await placeConfirmedCodOrder(shop);

    const staff = await browser.newContext({ baseURL: ADMIN });
    const page = await staff.newPage();
    await signInAsOwner(page);
    await pickAndPack(page, number);
    const waybill = await waitForWaybill(number);
    const cash = Number(sql(`SELECT "CASH_AMT" FROM "SHPG"."SHPG_MAIN" WHERE "ORDR_NO" = '${number}'`));
    expect(cash).toBeGreaterThan(0);
    await courierPush(page, waybill, 'DELIVERED');
    expect(sql(`SELECT "COD_STS_ID" FROM "SHPG"."SHPG_MAIN" WHERE "ORDR_NO" = '${number}'`)).toBe('2');

    // The overview lists the parcel as delivered, cash not yet paid (COD-1, COD-13).
    await page.goto('/dispatch/cod');
    await expect(page.getByRole('heading', { level: 1, name: 'COD reconciliation' })).toBeVisible();
    await expect(page.getByRole('row', { name: new RegExp(number) })).toBeVisible();
    await checkAccessible(page);

    // A total that is not the sum of the rows cannot be saved (COD-8).
    await page.goto('/dispatch/cod/remittances/new');
    const reference = `E2E-${Date.now()}`;
    await page.getByLabel('Statement reference').fill(reference);
    await page.getByLabel('Total on the statement (PKR)').fill('1');
    await page.getByLabel('Parcels (waybill, amount)').fill(`${waybill}, ${cash - 100}`);
    await expect(page.getByText('does not equal the rows')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Record the statement' })).toBeDisabled();
    await checkAccessible(page);

    // A short payment is recorded, flagged and the parcel is marked short (COD-3).
    await recordStatement(page, reference, cash - 100, `${waybill}, ${cash - 100}`);
    await expect(page.getByRole('heading', { level: 1, name: `Statement ${reference}` })).toBeVisible();
    await expect(page.getByRole('row', { name: new RegExp(waybill) })).toContainText('Short');
    expect(sql(`SELECT "COD_STS_ID" FROM "SHPG"."SHPG_MAIN" WHERE "ORDR_NO" = '${number}'`)).toBe('4');
    await checkAccessible(page);

    // The same reference again changes nothing and says so (COD-9); the parcel paid again is a duplicate (COD-4).
    await recordStatement(page, reference, cash, `${waybill}, ${cash}`);
    await expect(page.getByText('recorded before')).toBeVisible();
    expect(sql(`SELECT count(*) FROM "SHPG"."RMTN_MAIN" WHERE "REF_NO" = '${reference}'`)).toBe('1');
    await recordStatement(page, `${reference}-B`, cash, `${waybill}, ${cash}`);
    await expect(page.getByRole('row', { name: new RegExp(waybill) })).toContainText('Paid before');

    // The flagged line is resolved with a note: who and why are kept (COD-12).
    await page.goto('/dispatch/cod');
    const exception = page.getByRole('row', { name: new RegExp(`${waybill}.*Short`) });
    await exception.getByRole('textbox').fill('Courier kept the rider fee, agreed by phone');
    await exception.getByRole('button', { name: 'Resolve' }).click();
    await expect(page.getByText('The line is resolved')).toBeVisible();
    expect(sql(`SELECT "COD_STS_ID" FROM "SHPG"."SHPG_MAIN" WHERE "ORDR_NO" = '${number}'`)).toBe('6');
    expect(sql(`SELECT "RSLV_BY" IS NOT NULL FROM "SHPG"."RMTN_LINE" l JOIN "SHPG"."RMTN_MAIN" r ON r."ID" = l."RMTN_ID" WHERE r."REF_NO" = '${reference}'`)).toBe('t');

    await shop.close();
    await staff.close();
  });
});
