import { createHmac } from 'node:crypto';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { Page, expect } from '@playwright/test';

/**
 * Helpers for the identity journeys that need real people in the admin app: a signed-in Owner, accounts created through the screens,
 * and an "authenticator app" that works out the 6-digit codes. They talk to the app only through its pages, as a person would.
 */
export const OWNER = process.env['E2E_OWNER_USERNAME'] ?? 'e2e.owner';
export const OWNER_PASSWORD = process.env['E2E_OWNER_PASSWORD'];
/** Chosen by the Owner at the first sign-in of a run, so a second journey in the same run signs in with it. */
const OWNER_NEW_PASSWORD = 'e2e-owner-chosen-passphrase-2026-hj';
export const PERSON_PASSWORD = 'e2e-person-chosen-passphrase-2026-mt';

const BASE32 = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567';
const SECRET_FILE = '.e2e-state/owner-secret.json';

export function base32Decode(text: string): Buffer {
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

export function totp(secret: Buffer, step: number): string {
  const counter = Buffer.alloc(8);
  counter.writeBigInt64BE(BigInt(step));
  const hash = createHmac('sha1', secret).update(counter).digest();
  const offset = hash[hash.length - 1] & 0x0f;
  const binary = ((hash[offset] & 0x7f) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
  return (binary % 1_000_000).toString().padStart(6, '0');
}

const currentStep = (): number => Math.floor(Date.now() / 1000 / 30);

/** The newest step each secret has used: a code works once, and only for a newer step. */
const lastStepUsed = new Map<string, number>();

/** A code the server will accept: waits for the clock to reach a step newer than the last one used with this secret, as a person would. */
export async function nextCode(page: Page, secret: Buffer): Promise<string> {
  const key = secret.toString('hex');
  for (;;) {
    const step = currentStep();
    if (step > (lastStepUsed.get(key) ?? 0)) {
      lastStepUsed.set(key, step);
      return totp(secret, step);
    }
    await page.waitForTimeout(1000);
  }
}

export async function passwordStep(page: Page, user: string, password: string): Promise<void> {
  await page.goto('/sign-in');
  await page.getByLabel('User name').fill(user);
  await page.getByLabel('Password').fill(password);
  await page.getByRole('button', { name: 'Sign in' }).click();
}

export async function signOut(page: Page): Promise<void> {
  await page.getByRole('button', { name: 'Sign out', exact: true }).click();
  await expect(page).toHaveURL(/\/sign-in/);
}

/** On the change-password page: chooses the given password. */
export async function chooseOwnPassword(page: Page, current: string, next: string): Promise<void> {
  await expect(page).toHaveURL(/\/change-password/);
  await page.getByLabel('Current password').fill(current);
  await page.getByLabel('New password', { exact: true }).fill(next);
  await page.getByLabel('Type the new password again').fill(next);
  await page.getByRole('button', { name: 'Change password' }).click();
}

/** On the two-step setup page: "scans" the code, confirms with the first code, saves the recovery codes, and returns the secret. */
export async function setUpTwoStep(page: Page): Promise<Buffer> {
  await expect(page).toHaveURL(/\/setup-required/);
  await expect(page.locator('app-qr-code svg')).toBeVisible();
  const secret = base32Decode((await page.locator('code').first().innerText()).trim());
  await page.getByLabel('6-digit code').fill(await nextCode(page, secret));
  await page.getByRole('button', { name: 'Turn on two-step verification' }).click();
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Save your recovery codes');
  await page.getByLabel('I have saved these codes somewhere safe').check();
  await page.getByRole('button', { name: 'Continue' }).click();
  await expect(page.getByRole('navigation', { name: 'Primary' })).toBeVisible();
  return secret;
}

/** A person's first sign-in: password, forced change, two-step setup. Returns their authenticator secret. */
export async function firstSignIn(page: Page, user: string, temporaryPassword: string): Promise<Buffer> {
  await passwordStep(page, user, temporaryPassword);
  await chooseOwnPassword(page, temporaryPassword, PERSON_PASSWORD);
  return setUpTwoStep(page);
}

export async function signInWithCode(page: Page, user: string, password: string, secret: Buffer): Promise<void> {
  await passwordStep(page, user, password);
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Two-step verification');
  await page.getByLabel('Authentication code').fill(await nextCode(page, secret));
  await page.getByRole('button', { name: 'Verify' }).click();
  await expect(page.getByRole('navigation', { name: 'Primary' })).toBeVisible();
}

/**
 * Signs the page in as the Owner of this run. The first time (initial password from E2E_OWNER_PASSWORD) the Owner chooses a password and
 * sets up two-step verification, and the secret is kept for the rest of the run; later calls sign in with password and code.
 */
export async function signInAsOwner(page: Page): Promise<void> {
  if (!OWNER_PASSWORD) {
    throw new Error('E2E_OWNER_PASSWORD is not set');
  }

  if (existsSync(SECRET_FILE)) {
    const secret = Buffer.from(JSON.parse(readFileSync(SECRET_FILE, 'utf8')).secret as string, 'hex');
    await signInWithCode(page, OWNER, OWNER_NEW_PASSWORD, secret);
    return;
  }

  await passwordStep(page, OWNER, OWNER_PASSWORD);
  await chooseOwnPassword(page, OWNER_PASSWORD, OWNER_NEW_PASSWORD);
  const secret = await setUpTwoStep(page);
  mkdirSync('.e2e-state', { recursive: true });
  writeFileSync(SECRET_FILE, JSON.stringify({ secret: secret.toString('hex') }));
}

/** As the signed-in Owner: adds a staff member with one role and returns the temporary password the screen shows once. */
export async function createStaff(page: Page, userName: string, displayName: string, roleName: string): Promise<string> {
  await page.goto('/staff');
  await page.getByRole('button', { name: 'Add staff member' }).click();
  await page.getByLabel('User name').fill(userName);
  await page.getByLabel('Full name').fill(displayName);
  await page.getByLabel(roleName, { exact: false }).first().check();
  await page.getByRole('button', { name: 'Create account' }).click();
  const password = (await page.locator('#otp-value').innerText()).trim();
  expect(password.length).toBeGreaterThanOrEqual(16);
  await page.getByRole('button', { name: 'Done, I have saved it' }).click();
  return password;
}

/** The names of the links in the primary navigation. */
export async function menu(page: Page): Promise<string[]> {
  return (await page.getByRole('navigation', { name: 'Primary' }).getByRole('link').allInnerTexts()).map((t) => t.trim());
}

export const uniqueName = (prefix: string): string => `${prefix}.${Date.now().toString().slice(-7)}`;
