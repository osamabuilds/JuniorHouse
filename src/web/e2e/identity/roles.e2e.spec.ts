import { Page, expect, test } from '@playwright/test';
import { OWNER_PASSWORD, createStaff, firstSignIn, menu, signInAsOwner, signOut, uniqueName } from './support';

/**
 * Roles with real authentication (identity/roles, task R13): a Catalogue person edits the catalogue and meets "no access" everywhere
 * else; an Order-ops person can look at stock and cannot change it; a direct call without the permission fails whatever the screens
 * show. Needs an Owner whose password is E2E_OWNER_PASSWORD (see staff-accounts.e2e.spec.ts).
 */
test.describe('roles (needs E2E_OWNER_PASSWORD)', () => {
  test.skip(!OWNER_PASSWORD, 'set E2E_OWNER_PASSWORD to the Owner password created for this run');
  test.describe.configure({ mode: 'serial', timeout: 120_000 });

  const catalogue = uniqueName('e2e.cat');
  const orderOps = uniqueName('e2e.ops');
  const temporary: Record<string, string> = {};

  /** A call made by the page itself, with the session cookie and the anti-forgery header, as the admin app would. */
  async function call(page: Page, method: string, path: string): Promise<number> {
    return page.evaluate(
      async ({ method, path }) => {
        const me = (await (await fetch('/api/auth/me')).json()) as { csrfToken: string };
        const response = await fetch(path, {
          method,
          headers: { 'Content-Type': 'application/json', 'X-Romp-Csrf': me.csrfToken },
          body: method === 'GET' ? undefined : '{}',
        });
        return response.status;
      },
      { method, path },
    );
  }

  test('the Owner adds a Catalogue person and an Order-ops person, and sees the role matrix', async ({ page }) => {
    await signInAsOwner(page);
    temporary[catalogue] = await createStaff(page, catalogue, 'E2E Catalogue Person', 'Catalogue');
    temporary[orderOps] = await createStaff(page, orderOps, 'E2E Order Ops Person', 'Order operations');

    await page.getByRole('link', { name: 'Roles' }).click();
    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Roles and permissions');
    const table = page.getByRole('table', { name: 'Which role can do what' });
    await expect(table.getByRole('columnheader')).toContainText(['Permission', 'Owner', 'Catalogue', 'Order operations', 'Support', 'Marketing']);
    await expect(table.getByRole('row', { name: /inventory\.write/ }).getByRole('cell').nth(2)).toHaveText('No'); // Order-ops cannot write stock
    await signOut(page);
  });

  test('a Catalogue person: the menu shows their sections, other screens say "no access", and a direct call fails', async ({ page }) => {
    await firstSignIn(page, catalogue, temporary[catalogue]);

    expect(await menu(page)).toEqual(['Reference Data', 'Styles', 'Catalog', 'Warehouse', 'Storefront']);

    // A screen their roles do not cover is replaced by the "no access" page, not left to fail call by call.
    await page.goto('/vendors');
    await expect(page).toHaveURL(/\/forbidden/);
    await expect(page.getByRole('heading', { level: 1 })).toHaveText('You do not have access to this page');
    await page.goto('/staff');
    await expect(page).toHaveURL(/\/forbidden/);

    // Hiding is only a convenience: the server refuses direct calls, and allows the ones their roles cover.
    expect(await call(page, 'GET', '/api/purchase-orders/')).toBe(403);
    expect(await call(page, 'GET', '/api/identity/staff/')).toBe(403);
    expect(await call(page, 'POST', '/api/warehouse/adjustments/')).toBe(403);
    expect(await call(page, 'GET', '/api/catalog/styles/')).toBe(200);
    expect(await call(page, 'POST', '/api/catalog/styles/')).not.toBe(403); // allowed to try; the empty form is a 400
    await signOut(page);
  });

  test('an Order-ops person can look at stock but cannot change it', async ({ page }) => {
    await firstSignIn(page, orderOps, temporary[orderOps]);

    expect(await menu(page)).toEqual(['Reference Data', 'Styles', 'Catalog', 'Warehouse', 'Storefront']);
    await page.goto('/warehouse/stock');
    await expect(page).toHaveURL(/\/warehouse\/stock/);
    expect(await call(page, 'GET', '/api/warehouse/stock/')).toBe(200);
    expect(await call(page, 'GET', '/api/warehouse/adjustments/')).toBe(200);

    expect(await call(page, 'POST', '/api/warehouse/adjustments/')).toBe(403);
    expect(await call(page, 'POST', '/api/warehouse/cycle-counts/999999999/confirm')).toBe(403);
    expect(await call(page, 'PUT', '/api/warehouse/alerts/thresholds/999999999')).toBe(403);
    expect(await call(page, 'GET', '/api/purchase-orders/')).toBe(403); // cost and advance terms are not for this role
    await signOut(page);
  });

  test('the Owner gives the Catalogue person the Order-ops role too, and it applies on their next request', async ({ browser }) => {
    const owner = await browser.newPage();
    try {
      await signInAsOwner(owner);
      await owner.goto('/staff');
      await owner.getByRole('button', { name: `Open ${catalogue}` }).click();
      await owner.getByRole('group', { name: 'Roles' }).getByLabel('Order operations').check();
      await owner.getByRole('button', { name: 'Save roles' }).click();
      await expect(owner.getByText('Catalogue, Order operations')).toBeVisible();
    } finally {
      await owner.close();
    }
  });
});
