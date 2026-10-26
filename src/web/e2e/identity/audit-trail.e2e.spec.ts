import { expect, test } from '@playwright/test';
import { OWNER, OWNER_PASSWORD, createStaff, signInAsOwner, uniqueName } from './support';

/**
 * The audit trail with real authentication (identity/audit-trail, task 13): the Owner adds a person, and the Audit page shows that entry
 * with the Owner's name and the names of the changed columns, never the values. Entries arrive through the outbox, a few seconds after
 * the change, so the page is refreshed until the entry shows. Needs an Owner whose password is E2E_OWNER_PASSWORD.
 */
test.describe('audit trail (needs E2E_OWNER_PASSWORD)', () => {
  test.skip(!OWNER_PASSWORD, 'set E2E_OWNER_PASSWORD to the Owner password created for this run');
  test.describe.configure({ timeout: 120_000 });

  test('the Owner sees their own change, attributed to them, with column names and no values', async ({ page }) => {
    await signInAsOwner(page);
    const person = uniqueName('e2e.audited');
    const temporary = await createStaff(page, person, 'E2E Audited Person', 'Catalogue');

    await page.getByRole('link', { name: 'Audit', exact: true }).click();
    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Audit trail');

    // The newest account insert by this Owner; refresh the filter until the outbox has delivered it.
    await expect(async () => {
      await page.getByLabel('Person').fill(OWNER);
      await page.getByLabel('Record type').fill('USR_MAIN');
      await page.getByLabel('What happened').selectOption('INSERT');
      await page.getByRole('button', { name: 'Apply filters' }).click();
      await expect(page.getByRole('table').getByRole('row').nth(1)).toContainText(OWNER, { timeout: 2_000 });
    }).toPass({ timeout: 60_000 });

    await page.getByRole('table').getByRole('button', { name: /^Open entry/ }).first().click();
    await expect(page.getByRole('heading', { level: 2, name: /^Entry/ })).toBeVisible();
    await expect(page.getByText('never their values')).toBeVisible();
    await expect(page.locator('li code').first()).toBeVisible(); // column names, as codes

    // No secret and no typed value anywhere on the page.
    const html = await page.content();
    expect(html).not.toContain(temporary);
    expect(html.toLowerCase()).not.toContain('argon2');
  });
});
