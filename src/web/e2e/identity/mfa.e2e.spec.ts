import { createHmac } from 'node:crypto';
import { Page, expect, test } from '@playwright/test';

/**
 * Two-step verification with real authentication (identity/mfa, task 14). Same set-up as sessions.e2e.spec.ts: the API runs with
 * `Identity__UseRealAuthentication=true`, the admin app reaches it through its own origin, and an Owner exists whose password is
 * E2E_OWNER_PASSWORD (create it, or run `reset-owner --password --mfa`, before each run; without it the journey is skipped).
 *
 * A test helper plays the authenticator app: it reads the setup key from the page and computes the 6-digit codes itself.
 */
const OWNER = process.env['E2E_MFA_OWNER_USERNAME'] ?? 'e2e.mfa.owner';
const INITIAL_PASSWORD = process.env['E2E_MFA_OWNER_PASSWORD'];
const NEW_PASSWORD = 'e2e-mfa-new-passphrase-2026-pq';

const BASE32 = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567';

function base32Decode(text: string): Buffer {
  const clean = text.replace(/[\s=]/g, '').toUpperCase();
  let bits = 0;
  let value = 0;
  const out: number[] = [];
  for (const ch of clean) {
    value = (value << 5) | BASE32.indexOf(ch);
    bits += 5;
    if (bits >= 8) {
      out.push((value >>> (bits - 8)) & 0xff);
      bits -= 8;
    }
  }
  return Buffer.from(out);
}

/** RFC 6238: the 6-digit code of an authenticator app for one 30-second step. */
function totp(secret: Buffer, step: number): string {
  const counter = Buffer.alloc(8);
  counter.writeBigInt64BE(BigInt(step));
  const hash = createHmac('sha1', secret).update(counter).digest();
  const offset = hash[hash.length - 1] & 0x0f;
  const binary = ((hash[offset] & 0x7f) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
  return (binary % 1_000_000).toString().padStart(6, '0');
}

/** The newest step whose code the server has accepted (or that this test has used): a code works once, and only for a newer step. */
let lastStep = 0;
const currentStep = (): number => Math.floor(Date.now() / 1000 / 30);

/** A code the server will accept: waits for the clock to reach a step newer than the last one used, as a person would wait for the next code. */
async function nextCode(page: Page, secret: Buffer): Promise<string> {
  for (;;) {
    const step = currentStep();
    if (step > lastStep) {
      lastStep = step;
      return totp(secret, step);
    }
    await page.waitForTimeout(1000);
  }
}

async function passwordStep(page: Page, user: string, password: string): Promise<void> {
  await page.goto('/sign-in');
  await page.getByLabel('User name').fill(user);
  await page.getByLabel('Password').fill(password);
  await page.getByRole('button', { name: 'Sign in' }).click();
}

async function signOut(page: Page): Promise<void> {
  await page.getByRole('button', { name: 'Sign out', exact: true }).click();
  await expect(page).toHaveURL(/\/sign-in/);
}

test.describe('two-step verification (needs E2E_MFA_OWNER_PASSWORD)', () => {
  test.skip(!INITIAL_PASSWORD, 'set E2E_MFA_OWNER_PASSWORD to the Owner password created for this run');
  // Waiting for the next 30-second code is part of the journey, so each test may take up to a minute and a half.
  test.describe.configure({ mode: 'serial', timeout: 90_000 });

  let secret = Buffer.alloc(0);
  let recoveryCodes: string[] = [];

  test('first sign-in: choose a password, then set up two-step verification and save the recovery codes', async ({ page }) => {
    await passwordStep(page, OWNER, INITIAL_PASSWORD ?? '');
    await expect(page).toHaveURL(/\/change-password/);
    await page.getByLabel('Current password').fill(INITIAL_PASSWORD ?? '');
    await page.getByLabel('New password', { exact: true }).fill(NEW_PASSWORD);
    await page.getByLabel('Type the new password again').fill(NEW_PASSWORD);
    await page.getByRole('button', { name: 'Change password' }).click();

    // The session can do nothing else until two-step verification is on.
    await expect(page).toHaveURL(/\/setup-required/);
    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Set up two-step verification');
    await page.goto('/styles');
    await expect(page).toHaveURL(/\/setup-required/);

    // The QR code is drawn here, in the page; the same secret is offered as text.
    await expect(page.locator('app-qr-code svg')).toBeVisible();
    secret = base32Decode((await page.locator('code').first().innerText()).trim());
    expect(secret.length).toBe(20); // 160 bits

    // A wrong code is refused with a reason; the right one turns it on.
    await page.getByLabel('6-digit code').fill('000000');
    await page.getByRole('button', { name: 'Turn on two-step verification' }).click();
    await expect(page.getByRole('alert')).toContainText('not valid');
    lastStep = currentStep();
    await page.getByLabel('6-digit code').fill(totp(secret, lastStep));
    await page.getByRole('button', { name: 'Turn on two-step verification' }).click();

    // Recovery codes are shown once, and the person cannot move on until they say they saved them.
    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Save your recovery codes');
    recoveryCodes = (await page.locator('ol li').allInnerTexts()).map((c) => c.trim());
    expect(recoveryCodes).toHaveLength(10);
    await expect(page.getByRole('button', { name: 'Continue' })).toBeDisabled();
    await page.getByLabel('I have saved these codes somewhere safe').check();
    await page.getByRole('button', { name: 'Continue' }).click();

    // Now the whole admin area opens, and nothing about the codes was left in the browser.
    await expect(page.getByRole('navigation', { name: 'Primary' })).toBeVisible();
    expect(await page.evaluate(() => localStorage.length + sessionStorage.length)).toBe(0);
    await page.goto('/security');
    await expect(page.getByText('Two-step verification is on')).toBeVisible();
    await expect(page.getByText('10 recovery codes left')).toBeVisible();
    await signOut(page);
  });

  test('sign in with an authenticator code: a wrong one is refused, a code that was already used cannot be used again', async ({ page }) => {
    await passwordStep(page, OWNER, NEW_PASSWORD);
    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Two-step verification');

    await page.getByLabel('Authentication code').fill('000000');
    await page.getByRole('button', { name: 'Verify' }).click();
    await expect(page.getByRole('alert')).toContainText("That code didn't work");

    // The code that turned two-step verification on is spent, so the person waits for the next one.
    const code = await nextCode(page, secret);
    await page.getByLabel('Authentication code').fill(code);
    await page.getByRole('button', { name: 'Verify' }).click();
    await expect(page.getByRole('navigation', { name: 'Primary' })).toBeVisible();
    await signOut(page);

    // Someone who saw that code and tries it again, inside its validity window, is refused with the same generic message.
    await passwordStep(page, OWNER, NEW_PASSWORD);
    await page.getByLabel('Authentication code').fill(code);
    await page.getByRole('button', { name: 'Verify' }).click();
    await expect(page.getByRole('alert')).toContainText("That code didn't work");
    await expect(page.getByRole('alert')).not.toContainText(/used|replay|expired/i);
  });

  test('sign in with a recovery code, once', async ({ page }) => {
    await passwordStep(page, OWNER, NEW_PASSWORD);
    await page.getByRole('button', { name: 'Use a recovery code instead' }).click();
    await page.getByLabel('Recovery code').fill(recoveryCodes[0].toLowerCase());
    await page.getByRole('button', { name: 'Verify' }).click();
    await expect(page.getByRole('navigation', { name: 'Primary' })).toBeVisible();
    await page.goto('/security');
    await expect(page.getByText('9 recovery codes left')).toBeVisible();
    await signOut(page);

    // The same recovery code does not work a second time.
    await passwordStep(page, OWNER, NEW_PASSWORD);
    await page.getByRole('button', { name: 'Use a recovery code instead' }).click();
    await page.getByLabel('Recovery code').fill(recoveryCodes[0]);
    await page.getByRole('button', { name: 'Verify' }).click();
    await expect(page.getByRole('alert')).toContainText("That code didn't work");
  });

  test('new recovery codes need a current code, and the old ones stop working', async ({ page }) => {
    await passwordStep(page, OWNER, NEW_PASSWORD);
    await page.getByLabel('Authentication code').fill(await nextCode(page, secret));
    await page.getByRole('button', { name: 'Verify' }).click();
    await expect(page.getByRole('navigation', { name: 'Primary' })).toBeVisible();

    await page.goto('/security');
    await page.getByLabel('Current code from your authenticator app').fill(await nextCode(page, secret));
    await page.getByRole('button', { name: 'Get new recovery codes' }).click();
    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Save your recovery codes');
    const fresh = (await page.locator('ol li').allInnerTexts()).map((c) => c.trim());
    expect(fresh).toHaveLength(10);
    expect(fresh.filter((c) => recoveryCodes.includes(c))).toEqual([]);
    await page.getByLabel('I have saved these codes somewhere safe').check();
    await page.getByRole('button', { name: 'Continue' }).click();
    await expect(page.getByText('10 recovery codes left')).toBeVisible();
    await signOut(page);

    await passwordStep(page, OWNER, NEW_PASSWORD);
    await page.getByRole('button', { name: 'Use a recovery code instead' }).click();
    await page.getByLabel('Recovery code').fill(recoveryCodes[1]);
    await page.getByRole('button', { name: 'Verify' }).click();
    await expect(page.getByRole('alert')).toContainText("That code didn't work");
  });
});
