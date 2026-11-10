import AxeBuilder from '@axe-core/playwright';
import { Page, expect, test } from '@playwright/test';
import { OWNER_PASSWORD, signInAsOwner } from '../identity/support';
import { ADMIN, STOREFRONT, courierPush, pickAndPack, placeConfirmedCodOrder, sql, waitForWaybill } from './shipping-support';

/**
 * Reverse QC (S17; docs/specs/returns/reverse-qc): a parcel the courier returns becomes a case; staff receive it, inspect each line (restock, seconds or
 * reject with a note), and only completing the case announces the result.
 *
 * Needs the docker stack up and an Owner (see the identity journeys): E2E_OWNER_PASSWORD, STOREFRONT_BASE_URL, ADMIN_BASE_URL, API_BASE_URL.
 */
const AXE_TAGS = ['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'];

async function checkAccessible(page: Page): Promise<void> {
  const axe = await new AxeBuilder({ page }).withTags(AXE_TAGS).analyze();
  expect(axe.violations.map((v) => `${v.id}: ${v.nodes[0]?.html}`)).toEqual([]);
}

test.describe('Reverse QC', () => {
  test.skip(!OWNER_PASSWORD, 'set E2E_OWNER_PASSWORD to the Owner password created for this run');

  test('RQC-1, RQC-3, RQC-5 to RQC-8: a returned parcel is received, inspected, and only completing it announces the result', async ({ browser }) => {
    test.setTimeout(420_000);
    const shop = await browser.newPage({ baseURL: STOREFRONT });
    const number = await placeConfirmedCodOrder(shop);

    const staff = await browser.newContext({ baseURL: ADMIN });
    const page = await staff.newPage();
    await signInAsOwner(page);
    await pickAndPack(page, number);
    const waybill = await waitForWaybill(number);
    await courierPush(page, waybill, 'RETURNED_TO_ORIGIN');

    // The returned status opens one case for the order (RQC-1). The outbox worker delivers it, so wait for it.
    await expect.poll(() => sql(`SELECT count(*) FROM "RTRN"."RTRN_MAIN" WHERE "ORDR_NO" = '${number}'`), { timeout: 150_000, intervals: [3000] }).toBe('1');
    const bin = sql(`SELECT "BIN_CODE" FROM "WHSE"."BIN_MAIN" WHERE "ACT_IND" ORDER BY "ID" LIMIT 1`);
    expect(bin).not.toBe('');

    await page.goto('/returns');
    await expect(page.getByRole('heading', { level: 1, name: 'Returned parcels' })).toBeVisible();
    await page.getByRole('link', { name: number }).click();
    await expect(page.getByRole('heading', { level: 1, name: `Returned parcel for order ${number}` })).toBeVisible();
    await expect(page.getByText('Awaiting receipt')).toBeVisible();
    await checkAccessible(page);

    // The seal is not intact: nothing moves yet and the case waits for inspection (RQC-3).
    await page.getByLabel('Bin to put stock into').fill(bin);
    await page.getByRole('button', { name: 'Receive' }).click();
    await expect(page.getByText('Received. Now decide each line.')).toBeVisible();
    expect(sql(`SELECT "RTRN_STS_ID" FROM "RTRN"."RTRN_MAIN" WHERE "ORDR_NO" = '${number}'`)).toBe('2');
    await checkAccessible(page);

    // Rejecting needs a note (RQC-5); completing with a line waiting is refused and says how many (RQC-7).
    await page.getByRole('button', { name: 'Complete the case' }).click();
    await expect(page.getByRole('alert')).toContainText('still waiting for a decision');
    await page.getByLabel(/^Decision for /).first().selectOption('REJECT');
    await page.getByRole('button', { name: 'Decide' }).first().click();
    await expect(page.getByRole('alert')).toContainText('why it is rejected');
    await page.getByLabel(/^Note for /).first().fill('Worn and washed');
    await page.getByRole('button', { name: 'Decide' }).first().click();
    await expect(page.getByText('The line is decided.')).toBeVisible();

    // No result is announced before completion (RQC-8); completing announces it once.
    expect(sql(`SELECT count(*) FROM "RTRN"."OUTB_MSG" WHERE "EVNT_TYP" = 'ReturnQcCompletedEvent' AND "PYLD"::text LIKE '%${number}%'`)).toBe('0');
    const lines = Number(sql(`SELECT count(*) FROM "RTRN"."RTRN_LINE" l JOIN "RTRN"."RTRN_MAIN" m ON m."ID" = l."RTRN_ID" WHERE m."ORDR_NO" = '${number}'`));
    for (let i = 1; i < lines; i += 1) {
      await page.getByRole('button', { name: 'Decide' }).first().click();
    }
    await page.getByRole('button', { name: 'Complete the case' }).click();
    await expect(page.getByText('The case is complete.')).toBeVisible();
    expect(sql(`SELECT "RTRN_STS_ID" FROM "RTRN"."RTRN_MAIN" WHERE "ORDR_NO" = '${number}'`)).toBe('3');
    await checkAccessible(page);

    await shop.close();
    await staff.close();
  });
});
