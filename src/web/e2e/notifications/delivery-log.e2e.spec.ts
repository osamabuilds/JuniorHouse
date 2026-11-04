import { execSync } from 'node:child_process';
import { expect, test } from '@playwright/test';

/**
 * Notifications, the delivery log (docs/specs/notifications/delivery-log, DLG-1, DLG-2, DLG-5, DLG-6, DLG-7, DLG-8, DLG-9). Support open the log for
 * one order, see a failed message with its recipient masked, resend it after one confirmation, and see the new copy queued.
 * The failed delivery is written straight into the local database with psql (the worker only creates them from real order events), so this runs
 * against the docker stack only: `docker compose -f docker-compose.yml -f docker-compose.secure.yml up -d --build`, then
 * `npx playwright test e2e/notifications`. The order id is made up, so the row shows "Unknown order" and no recipient.
 */
test.describe.configure({ timeout: 120_000 });

function sql(statement: string): void {
  const user = process.env['POSTGRES_USER'] ?? 'romp';
  const db = process.env['POSTGRES_DB'] ?? 'romp';
  execSync(`docker compose exec -T postgres psql -U ${user} -d ${db} -c "${statement.replace(/"/g, '\\"')}"`, { stdio: 'pipe' });
}

/** One notification for a made-up order with one email delivery in the given lookup status (1 queued, 2 sent, 4 failed). */
function seedDelivery(orderId: number, statusId: number): void {
  sql(
    `WITH n AS (INSERT INTO "NTFY"."NTFY_MAIN" ("NTFY_KIND_ID","REF_KIND_ID","REF_ID","EVNT_ID","EXPY_DTE","PRIO","INSR_DTE","INSR_BY") ` +
      `VALUES (1,1,${orderId},gen_random_uuid(),now() + interval '7 days',0,now(),'e2e') RETURNING "ID") ` +
      `INSERT INTO "NTFY"."NTFY_CHNL" ("NTFY_ID","CHNL_ID","CHNL_STS_ID","ATMP_CNT","ERR_CATG_ID","INSR_DTE","INSR_BY") ` +
      `SELECT "ID",1,${statusId},2,${statusId === 4 ? 2 : 'NULL'},now(),'e2e' FROM n`,
  );
}

const uniqueOrderId = (): number => 900_000_000 + Math.floor(Math.random() * 99_000_000);

test.describe('Notifications: delivery log', () => {
  test('a failed message is found by its order, shows its problem, and is resent after one confirmation', async ({ page }) => {
    const orderId = uniqueOrderId();
    seedDelivery(orderId, 4);

    await page.goto(`/notifications?orderId=${orderId}`);
    await expect(page.getByRole('heading', { level: 1, name: 'Notifications' })).toBeVisible();
    await expect(page.getByRole('region', { name: /1 message/ })).toBeVisible();

    const row = page.getByRole('row').filter({ hasText: 'Order confirmation' });
    await expect(row).toContainText('Email');
    await expect(row).toContainText('Failed');
    await expect(row).toContainText('Refused by the provider');
    await expect(row).not.toContainText('@'); // DLG-6: no address, masked or not, when the order is unknown

    await row.getByRole('button', { name: /Order confirmation/ }).click();
    await expect(page.getByRole('heading', { level: 2, name: /Order confirmation · Email/ })).toBeVisible();

    await page.getByRole('button', { name: 'Resend…' }).click();
    await expect(page.getByRole('alert').filter({ hasText: 'Send this message again?' })).toBeVisible();
    await page.getByRole('button', { name: 'Yes, resend it' }).click();

    await expect(page.getByRole('status').filter({ hasText: 'queued' })).toBeVisible();
    await expect(page.getByRole('row').filter({ hasText: 'Order confirmation' })).toHaveCount(2); // the original and the resend
    await expect(page.getByText('Resent').first()).toBeVisible();
  });

  test('a message that was already sent warns of a duplicate before it is resent', async ({ page }) => {
    const orderId = uniqueOrderId();
    seedDelivery(orderId, 2);

    await page.goto(`/notifications?orderId=${orderId}`);
    await page.getByRole('row').filter({ hasText: 'Order confirmation' }).getByRole('button').click();
    await page.getByRole('button', { name: 'Resend…' }).click();

    await expect(page.getByRole('alert').filter({ hasText: 'second copy' })).toBeVisible();
    await page.getByRole('button', { name: 'No, leave it' }).click();
    await expect(page.getByRole('row').filter({ hasText: 'Order confirmation' })).toHaveCount(1); // nothing was sent
  });

  test('the filters narrow the log and a search that matches nothing says so', async ({ page }) => {
    const orderId = uniqueOrderId();
    seedDelivery(orderId, 4);

    await page.goto(`/notifications?orderId=${orderId}`);
    await page.getByLabel('Status').selectOption({ label: 'Sent' });
    await expect(page.getByText('No messages match.')).toBeVisible();

    await page.getByLabel('Status').selectOption({ label: 'Failed' });
    await expect(page.getByRole('row').filter({ hasText: 'Order confirmation' })).toHaveCount(1);

    await page.getByRole('button', { name: 'Clear filters' }).click();
    await page.getByLabel('Search').fill('RMP-0000-NOT-AN-ORDER');
    await page.getByRole('button', { name: 'Search' }).click();
    await expect(page.getByText('No messages match.')).toBeVisible();
  });
});
