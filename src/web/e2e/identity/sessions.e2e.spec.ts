import AxeBuilder from '@axe-core/playwright';
import { Page, expect, test } from '@playwright/test';

/**
 * Staff sign-in with real authentication (identity/sessions, task 17). The API must run with `Identity__UseRealAuthentication=true`
 * (and `Identity__AllowInsecureCookies=true` when the stack is plain HTTP), and the admin app must reach it through its own origin
 * (nginx or `ng serve`), so the cookies are first-party.
 *
 * Before each run create or reset an Owner whose password is E2E_OWNER_PASSWORD (a first sign-in must change it):
 *   ROMP_OWNER_USERNAME=e2e.owner ROMP_OWNER_DISPLAY_NAME="E2E Owner" ROMP_OWNER_PASSWORD=<password> dotnet Romp.Api.dll create-owner
 *   (or `reset-owner --password` when the Owner exists). Without E2E_OWNER_PASSWORD the signed-in tests are skipped.
 * Two-step verification is not built yet, so every account ends on the "two-step required" page: that is what these tests expect.
 */
const OWNER = process.env['E2E_OWNER_USERNAME'] ?? 'e2e.owner';
const INITIAL_PASSWORD = process.env['E2E_OWNER_PASSWORD'];
const NEW_PASSWORD = 'e2e-new-passphrase-2026-xk';

const noScroll = async (page: Page) => page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth);

async function signIn(page: Page, user: string, password: string): Promise<void> {
  await page.getByLabel('User name').fill(user);
  await page.getByLabel('Password').fill(password);
  await page.getByRole('button', { name: 'Sign in' }).click();
}

test.describe('sign-in page (no account needed)', () => {
  test('has one heading, labelled controls, and no automated accessibility violations', async ({ page }) => {
    await page.goto('/sign-in');

    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Sign in');
    await expect(page.getByLabel('User name')).toBeVisible();
    await expect(page.getByLabel('Password')).toHaveAttribute('type', 'password');
    expect(page.url()).toContain('/sign-in');
    const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag22aa']).analyze();
    expect(results.violations.filter((v) => v.impact === 'serious' || v.impact === 'critical')).toEqual([]);
  });

  for (const viewport of [{ width: 360, height: 740 }, { width: 768, height: 900 }, { width: 1280, height: 800 }]) {
    test(`fits ${viewport.width}px wide without sideways scrolling`, async ({ page }) => {
      await page.setViewportSize(viewport);
      await page.goto('/sign-in');

      await expect(page.getByRole('button', { name: 'Sign in' })).toBeVisible();
      expect(await noScroll(page)).toBe(true);
    });
  }

  test('a visitor who is not signed in is sent to sign in, and the staff navigation is never shown', async ({ page }) => {
    await page.goto('/styles');

    await expect(page).toHaveURL(/\/sign-in\?returnUrl=%2Fstyles/);
    await expect(page.getByRole('navigation', { name: 'Primary' })).toHaveCount(0);
  });

  test('a failed sign-in gives one generic message, takes focus, and writes nothing to browser storage', async ({ page }) => {
    await page.goto('/sign-in');

    await signIn(page, 'nobody.at.all', 'wrong-passphrase-123');

    const alert = page.getByRole('alert');
    await expect(alert).toContainText("That user name and password didn't work");
    await expect(alert).toBeFocused();
    await expect(page.getByLabel('Password')).toHaveValue('');
    const stored = await page.evaluate(() => ({ local: localStorage.length, session: sessionStorage.length, cookie: document.cookie }));
    expect(stored).toEqual({ local: 0, session: 0, cookie: '' });
  });

  test('repeated failures show how long to wait, and nothing about why', async ({ page }) => {
    await page.goto('/sign-in');
    const name = `throttle.${Date.now().toString().slice(-8)}`;

    for (let attempt = 0; attempt < 3; attempt++) {
      await signIn(page, name, `wrong-passphrase-${attempt}`);
      await expect(page.getByRole('alert')).toContainText("didn't work");
    }
    await signIn(page, name, 'wrong-passphrase-3');

    await expect(page.getByRole('alert')).toContainText(/Try again in \d+ (seconds?|minutes?)/);
    await expect(page.getByRole('alert')).not.toContainText(/locked|no such|unknown|inactive/i);
  });
});

test.describe('signed-in flow (needs E2E_OWNER_PASSWORD)', () => {
  test.skip(!INITIAL_PASSWORD, 'set E2E_OWNER_PASSWORD to the Owner password created for this run');
  test.describe.configure({ mode: 'serial' });

  test('sign in, silent refresh, expiry, sign-out', async ({ page, context }) => {
    // 1. A first sign-in with the initial password must go and choose a password; the cookies are HttpOnly and SameSite=Strict.
    await page.goto('/sign-in');
    await signIn(page, OWNER, INITIAL_PASSWORD ?? '');
    await expect(page).toHaveURL(/\/change-password/);
    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Choose a new password');
    await expect(page.getByText('You need to choose your own password')).toBeVisible();

    const cookies = await context.cookies();
    const session = cookies.filter((c) => /^(__Secure-)?romp-(acs|rfs)$/.test(c.name));
    expect(session.map((c) => c.name.replace('__Secure-', '')).sort()).toEqual(['romp-acs', 'romp-rfs']);
    for (const cookie of session) {
      expect(cookie.httpOnly).toBe(true);
      expect(cookie.sameSite).toBe('Strict');
    }
    expect(await page.evaluate(() => document.cookie)).toBe(''); // no script can read them
    expect(await page.evaluate(() => localStorage.length + sessionStorage.length)).toBe(0);

    // 2. The access cookie has lapsed (simulated by deleting it): the first call fails, the app renews the session once and
    //    repeats it, and the person never notices.
    await context.clearCookies({ name: session.find((c) => c.name.endsWith('romp-acs'))?.name });
    await page.getByLabel('Current password').fill(INITIAL_PASSWORD ?? '');
    await page.getByLabel('New password', { exact: true }).fill(NEW_PASSWORD);
    await page.getByLabel('Type the new password again').fill(NEW_PASSWORD);
    const refreshed = page.waitForResponse((r) => r.url().endsWith('/api/auth/refresh') && r.request().method() === 'POST');
    await page.getByRole('button', { name: 'Change password' }).click();
    expect((await refreshed).status()).toBe(204);

    // 3. Two-step verification does not exist yet, so the session is held on its notice page and cannot reach any other screen.
    await expect(page).toHaveURL(/\/setup-required/);
    await expect(page.getByRole('heading', { level: 1 })).toContainText('Two-step verification');
    await page.goto('/styles');
    await expect(page).toHaveURL(/\/setup-required/);

    // 4. Sign out ends the session: cookies are gone and every staff page asks for sign-in again.
    await page.getByRole('button', { name: 'Sign out' }).click();
    await expect(page).toHaveURL(/\/sign-in/);
    const after = await context.cookies();
    expect(after.filter((c) => /romp-(acs|rfs)$/.test(c.name) && c.value !== '')).toEqual([]);
    await page.goto('/styles');
    await expect(page).toHaveURL(/\/sign-in\?returnUrl=%2Fstyles/);

    // 5. The new password works; the old one no longer does.
    await page.goto('/sign-in');
    await signIn(page, OWNER, INITIAL_PASSWORD ?? '');
    await expect(page.getByRole('alert')).toContainText("didn't work");
    await signIn(page, OWNER, NEW_PASSWORD);
    await expect(page).toHaveURL(/\/setup-required/);
  });
});
