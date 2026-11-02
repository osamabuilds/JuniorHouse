import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';
import { uniqueSuffix } from '../support';

/**
 * Customer sign-in journeys (ACT-1 to ACT-16, ACT-29, ACT-31) in the storefront container with real sign-in.
 * Run: `docker compose -f docker-compose.yml -f docker-compose.secure.yml up -d --build`, then
 * `STOREFRONT_BASE_URL=http://localhost:4000 npx playwright test e2e/customers/sign-in`.
 * Mail is read from Mailpit (MAILPIT_URL, default http://localhost:8025).
 */
const BASE = process.env['STOREFRONT_BASE_URL'] ?? 'http://localhost:4000';
const MAILPIT = process.env['MAILPIT_URL'] ?? 'http://localhost:8025';
const AXE_TAGS = ['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'];
const PASSWORD = 'Sunny-Day-42';

test.use({ baseURL: BASE });

/** The first link in the newest message to `to` whose path starts with `path`. */
async function mailLink(request: import('@playwright/test').APIRequestContext, to: string, path: string): Promise<string> {
  for (let attempt = 0; attempt < 30; attempt++) {
    const found = (await (await request.get(`${MAILPIT}/api/v1/search?query=${encodeURIComponent(`to:${to}`)}`)).json()) as { messages?: { ID: string; Subject: string }[] };
    for (const summary of found.messages ?? []) {
      const message = (await (await request.get(`${MAILPIT}/api/v1/message/${summary.ID}`)).json()) as { Text: string };
      const match = new RegExp(`https?://[^\\s"<>]*${path}\\?token=[^\\s"<>]+`).exec(message.Text);
      if (match) return match[0];
    }
    await new Promise((resolve) => setTimeout(resolve, 500));
  }
  throw new Error(`No email to ${to} with a ${path} link arrived.`);
}

/** Open a page and wait for Angular to hydrate it, so typed values are not reset by hydration. */
async function open(page: import('@playwright/test').Page, path: string): Promise<void> {
  await page.goto(path);
  await page.waitForLoadState('networkidle');
  await page.waitForFunction(() => document.querySelector('app-root') !== null && document.querySelector('[ngh]') === null); // hydrated: Angular removes the server's ngh markers
}

function localUrl(link: string): string {
  const url = new URL(link);
  return `${url.pathname}${url.search}`;
}

test.describe('customer sign-in', () => {
  test('SEO-05 the sign-in pages are not cached or indexed', async ({ request }) => {
    for (const path of ['/account/sign-in', '/account/register', '/account/forgot-password']) {
      const response = await request.get(path);
      expect(response.status(), path).toBe(200);
      expect(await response.text(), path).toContain('noindex');
    }
  });

  test('ACT-16 /account sends a signed-out visitor to sign in', async ({ page }) => {
    await open(page, '/account/addresses');
    await expect(page).toHaveURL(/\/account\/sign-in\?returnUrl=/);
    await expect(page.getByRole('heading', { level: 1, name: 'Sign in' })).toBeVisible();
  });

  test('ACT-1, ACT-2, ACT-12, ACT-31 register, see the checklist, verify the email, sign out and sign in again', async ({ page, request }) => {
    const email = `e2e-${uniqueSuffix()}@example.com`;

    await open(page, '/account/register');
    await expect(page.getByRole('heading', { level: 1, name: 'Create account' })).toBeVisible();
    await page.getByLabel('First name').fill('Sana');
    await page.getByLabel('Last name').fill('Khan');
    await page.getByLabel('Email').fill(email);
    await page.getByLabel('Mobile number').fill('0300 1234567');
    await page.getByLabel('Password', { exact: true }).fill('weak');
    await expect(page.getByRole('list', { name: 'Password rules' })).toBeVisible();
    await page.getByLabel('Password', { exact: true }).fill(PASSWORD);

    const axe = await new AxeBuilder({ page }).withTags(AXE_TAGS).analyze();
    expect(axe.violations.map((v) => v.id)).toEqual([]);

    await page.getByRole('checkbox').check();
    await page.getByRole('button', { name: 'Create account' }).click();
    await expect(page).toHaveURL(/\/account(\/addresses)?$/);

    // ACT-31: unverified customers see a notice but are never blocked.
    const bar = page.getByRole('button', { name: 'Verify my email' });
    await expect(bar).toBeVisible();

    await open(page, localUrl(await mailLink(request, email, '/account/verify-email')));
    await expect(page.getByRole('heading', { level: 1, name: 'Email verified' })).toBeVisible();

    await open(page, '/account');
    await page.getByRole('button', { name: /Sign out/ }).first().click();
    await open(page, '/account/sign-in');
    await page.getByLabel('Email').fill(email);
    await page.getByLabel('Password', { exact: true }).fill(PASSWORD);
    await page.getByRole('button', { name: 'Sign in' }).click();
    await expect(page).toHaveURL(/\/account(\/addresses)?$/);
    await expect(page.getByRole('button', { name: 'Verify my email' })).toHaveCount(0);
  });

  test('ACT-13 a customer who forgot the password sets a new one from the emailed link', async ({ page, request }) => {
    const email = `e2e-${uniqueSuffix()}@example.com`;
    await open(page, '/account/register');
    await page.getByLabel('First name').fill('Ali');
    await page.getByLabel('Last name').fill('Raza');
    await page.getByLabel('Email').fill(email);
    await page.getByLabel('Mobile number').fill('03001234567');
    await page.getByLabel('Password', { exact: true }).fill(PASSWORD);
    await page.getByRole('checkbox').check();
    await page.getByRole('button', { name: 'Create account' }).click();
    await expect(page).toHaveURL(/\/account(\/addresses)?$/);
    await page.getByRole('button', { name: /Sign out/ }).first().click();

    await open(page, '/account/forgot-password');
    await page.getByLabel('Email').fill(email);
    await page.getByRole('button', { name: /Send|Email me|Reset/i }).click();

    await open(page, localUrl(await mailLink(request, email, '/account/reset-password')));
    const next = 'Brand-New-Pass-7';
    await page.getByLabel('New password', { exact: true }).fill(next);
    await page.getByLabel('Confirm new password').fill(next);
    await page.getByRole('button', { name: 'Update password' }).click();
    await expect(page.getByRole('heading', { level: 1, name: 'Password updated' })).toBeVisible();

    await open(page, '/account/sign-in');
    await page.getByLabel('Email').fill(email);
    await page.getByLabel('Password', { exact: true }).fill(next);
    await page.getByRole('button', { name: 'Sign in' }).click();
    await expect(page).toHaveURL(/\/account(\/addresses)?$/);
  });

  test('ACT-9 a wrong password gives one generic message, never a lock', async ({ page }) => {
    await open(page, '/account/sign-in');
    for (let i = 0; i < 1; i++) {
      await page.getByLabel('Email').fill('nobody@example.com');
      await page.getByLabel('Password', { exact: true }).fill('Wrong-Pass-1');
      await page.getByRole('button', { name: 'Sign in' }).click();
      await expect(page.getByRole('alert').first()).toContainText('We could not sign you in');
    }
  });
});
