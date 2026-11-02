import { Page, expect } from '@playwright/test';
import { uniqueSuffix } from '../support';

export const CUSTOMER_PASSWORD = 'Sunny-Day-42';

/** Open a page and wait for Angular to hydrate it, so typed values are not reset by hydration. */
export async function openHydrated(page: Page, path: string): Promise<void> {
  await page.goto(path);
  await page.waitForLoadState('networkidle');
}

/** Creates a fresh customer through the register page; the browser is left signed in. Returns the email used. */
export async function registerCustomer(page: Page, firstName = 'Sana'): Promise<string> {
  const email = `e2e-${uniqueSuffix()}@example.com`;
  await openHydrated(page, '/account/register');
  await page.getByLabel('First name').fill(firstName);
  await page.getByLabel('Last name').fill('Khan');
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Mobile number').fill('03001234567');
  await page.getByLabel('Password', { exact: true }).fill(CUSTOMER_PASSWORD);
  await page.getByRole('checkbox').check();
  await page.getByRole('button', { name: 'Create account' }).click();
  await expect(page).toHaveURL(/\/account(\/addresses)?$/);
  return email;
}
