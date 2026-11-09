import { expect, test } from '@playwright/test';
import { OWNER_PASSWORD, PERSON_PASSWORD, createStaff, firstSignIn, menu, signInAsOwner, signInWithCode, signOut, uniqueName } from './support';

/**
 * Staff accounts with real authentication (identity/staff-accounts, task 13): the Owner adds a person, the person signs in with the
 * temporary password, is made to choose their own and to set up two-step verification, and when the Owner deactivates them their next
 * request fails. Needs an Owner whose password is E2E_OWNER_PASSWORD (create one, or run `reset-owner --password --mfa`, before the run).
 */
test.describe('staff accounts (needs E2E_OWNER_PASSWORD)', () => {
  test.skip(!OWNER_PASSWORD, 'set E2E_OWNER_PASSWORD to the Owner password created for this run');
  test.describe.configure({ mode: 'serial', timeout: 120_000 });

  const person = uniqueName('e2e.staff');
  let temporary = '';
  let secret = Buffer.alloc(0);

  test('the Owner adds a person; the password is shown once and is not on the list or the detail page', async ({ page }) => {
    await signInAsOwner(page);

    temporary = await createStaff(page, person, 'E2E Staff Person', 'Catalogue');

    await expect(page.getByText(temporary)).toHaveCount(0); // dismissed: it cannot be brought back
    await page.getByRole('button', { name: `Open ${person}` }).click();
    await expect(page.getByRole('heading', { level: 2, name: 'E2E Staff Person' })).toBeVisible();
    await expect(page.getByText('A temporary password is outstanding')).toBeVisible();
    expect(await page.content()).not.toContain(temporary);
    await signOut(page);
  });

  test('the person signs in with the temporary password, chooses their own and sets up two-step verification', async ({ page }) => {
    secret = await firstSignIn(page, person, temporary);

    // A Catalogue person: their own roles decide the menu, and the temporary password no longer works.
    expect(await menu(page)).toEqual(['Reference Data', 'Styles', 'Catalog', 'Warehouse', 'Storefront']);
    await signOut(page);
    await page.goto('/sign-in');
    await page.getByLabel('User name').fill(person);
    await page.getByLabel('Password').fill(temporary);
    await page.getByRole('button', { name: 'Sign in' }).click();
    await expect(page.getByRole('alert')).toContainText("didn't work");
  });

  test('when the Owner deactivates them, their next request fails and they cannot sign in again', async ({ browser }) => {
    const owner = await browser.newPage();
    const staff = await browser.newPage();
    try {
      await signInWithCode(staff, person, PERSON_PASSWORD, secret);
      await expect(staff.getByRole('navigation', { name: 'Primary' })).toBeVisible();

      await signInAsOwner(owner);
      await owner.goto('/staff');
      await owner.getByRole('button', { name: `Open ${person}` }).click();
      await owner.getByRole('button', { name: 'Deactivate' }).click();
      await expect(owner.getByText('Deactivated')).toBeVisible();

      // The next request of the person's open session is refused and they are sent to sign in.
      await staff.goto('/styles');
      await expect(staff).toHaveURL(/\/sign-in/);

      await staff.getByLabel('User name').fill(person);
      await staff.getByLabel('Password').fill(PERSON_PASSWORD);
      await staff.getByRole('button', { name: 'Sign in' }).click();
      await expect(staff.getByRole('alert')).toContainText("didn't work");
    } finally {
      await owner.close();
      await staff.close();
    }
  });
});
