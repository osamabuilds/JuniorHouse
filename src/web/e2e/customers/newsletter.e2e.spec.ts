import AxeBuilder from '@axe-core/playwright';
import { APIRequestContext, Page, expect, test } from '@playwright/test';
import { CUSTOMER_PASSWORD, openHydrated } from './customer-session';
import { uniqueSuffix } from '../support';

/**
 * The newsletter journeys in the storefront container (NLS-1 to NLS-6, TC-ACC-17): subscribe from the footer with JavaScript and without
 * it, read the welcome in Mailpit, unsubscribe from its link, see that an unticked registration box does not bring the address back, and
 * turn the preference on and off in the account.
 * Run: `docker compose -f docker-compose.yml -f docker-compose.secure.yml up -d --build`, then
 * `STOREFRONT_BASE_URL=http://localhost:4000 npx playwright test e2e/customers/newsletter`.
 */
const BASE = process.env['STOREFRONT_BASE_URL'] ?? 'http://localhost:4000';
const MAILPIT = process.env['MAILPIT_URL'] ?? 'http://localhost:8025';
const AXE_TAGS = ['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'];

test.use({ baseURL: BASE });

interface MailSummary { ID: string; Subject: string }

/** Messages to `to` whose subject contains `subject`, read from Mailpit. */
async function messagesTo(request: APIRequestContext, to: string, subject: string): Promise<MailSummary[]> {
  const found = (await (await request.get(`${MAILPIT}/api/v1/search?query=${encodeURIComponent(`to:${to}`)}`)).json()) as { messages?: MailSummary[] };
  return (found.messages ?? []).filter((m) => m.Subject.includes(subject));
}

/** Waits for the welcome and returns its text and the unsubscribe link inside. */
async function welcome(request: APIRequestContext, to: string): Promise<{ text: string; link: string; headers: Record<string, string[]> }> {
  for (let attempt = 0; attempt < 40; attempt++) {
    const [summary] = await messagesTo(request, to, 'Welcome to the Romp newsletter');
    if (summary) {
      const message = (await (await request.get(`${MAILPIT}/api/v1/message/${summary.ID}`)).json()) as { Text: string };
      const headers = (await (await request.get(`${MAILPIT}/api/v1/message/${summary.ID}/headers`)).json()) as Record<string, string[]>;
      const link = /https?:\/\/[^\s"<>]*\/newsletter\/unsubscribe\?token=[^\s"<>]+/.exec(message.Text)?.[0];
      if (link) return { text: message.Text, link, headers };
    }
    await new Promise((resolve) => setTimeout(resolve, 500));
  }
  throw new Error(`No welcome email to ${to} arrived.`);
}

const localUrl = (link: string): string => {
  const url = new URL(link);
  return `${url.pathname}${url.search}`;
};

async function subscribeInFooter(page: Page, email: string): Promise<void> {
  await page.getByLabel('Email address').fill(email);
  await page.getByRole('button', { name: 'Join' }).click();
}

test.describe('newsletter', () => {
  test('NLS-1 NLS-3 NLS-6 a visitor subscribes in the footer, gets one welcome, and its link unsubscribes them', async ({ page, request }) => {
    const email = `e2e-nl-${uniqueSuffix()}@example.com`;
    await openHydrated(page, '/');
    await subscribeInFooter(page, email);
    await expect(page.getByRole('status').filter({ hasText: 'Thank you' })).toBeVisible();

    // The same address again gets the same answer, and no second welcome is sent.
    await subscribeInFooter(page, email.toUpperCase());
    await expect(page.getByRole('status').filter({ hasText: 'Thank you' })).toBeVisible();

    const mail = await welcome(request, email);
    expect(mail.text).not.toContain(email);
    expect(JSON.stringify(mail.headers)).toContain('List-Unsubscribe');
    await new Promise((resolve) => setTimeout(resolve, 1500));
    expect(await messagesTo(request, email, 'Welcome to the Romp newsletter')).toHaveLength(1);

    // Opening the link signed out unsubscribes, and says so on a page nobody should index.
    const response = await page.goto(localUrl(mail.link));
    expect(response?.status()).toBe(200);
    await expect(page).toHaveURL(/\/newsletter\/unsubscribed\?status=ok$/);
    await expect(page.getByRole('heading', { level: 1, name: 'You are unsubscribed' })).toBeVisible();
    expect(await page.locator('meta[name=robots]').getAttribute('content')).toContain('noindex');

    // The link is safe to open again.
    await page.goto(localUrl(mail.link));
    await expect(page.getByRole('heading', { level: 1, name: 'You are unsubscribed' })).toBeVisible();

    const axe = await new AxeBuilder({ page }).withTags(AXE_TAGS).analyze();
    expect(axe.violations.map((v) => v.id)).toEqual([]);
  });

  test('NLS-6 a form post without JavaScript subscribes and lands on a confirmation page', async ({ browser, request }) => {
    const context = await browser.newContext({ javaScriptEnabled: false, baseURL: BASE });
    const page = await context.newPage();
    const email = `e2e-nl-nojs-${uniqueSuffix()}@example.com`;
    await page.goto('/');
    await page.getByLabel('Email address').fill(email);
    await page.getByRole('button', { name: 'Join' }).click();
    await expect(page).toHaveURL(/\/newsletter\/subscribed\?status=ok$/);
    await expect(page.getByRole('heading', { level: 1, name: 'You are subscribed' })).toBeVisible();
    await welcome(request, email);
    await context.close();
  });

  test('NLS-1 an unreadable address is refused with a message and nothing is sent', async ({ page, request }) => {
    await openHydrated(page, '/');
    const field = page.getByLabel('Email address');
    await field.fill('not-an-address');
    await page.getByRole('button', { name: 'Join' }).click();
    // The browser's own check stops it before any request.
    expect(await field.evaluate((e) => (e as HTMLInputElement).validity.valid)).toBe(false);
    const response = await request.post('/customer-api/newsletter/subscribe', { data: { email: 'not-an-address' }, headers: { origin: BASE } });
    expect(response.status()).toBe(400);
  });

  test('NLS-3 a forged unsubscribe link says it is not valid', async ({ page }) => {
    await page.goto('/newsletter/unsubscribe?token=MQ.AAAA');
    await expect(page).toHaveURL(/\/newsletter\/unsubscribed\?status=invalid$/);
    await expect(page.getByRole('heading', { level: 1, name: 'This link is not valid' })).toBeVisible();
  });

  test('NLS-4 NLS-2 TC-ACC-17 registering with the box unticked does not bring an unsubscribed address back; the account toggle does', async ({ page, request }) => {
    const email = `e2e-nl-reg-${uniqueSuffix()}@example.com`;

    // Subscribe, then unsubscribe from the emailed link.
    await openHydrated(page, '/');
    await subscribeInFooter(page, email);
    await expect(page.getByRole('status').filter({ hasText: 'Thank you' })).toBeVisible();
    await page.goto(localUrl((await welcome(request, email)).link));
    await expect(page.getByRole('heading', { level: 1, name: 'You are unsubscribed' })).toBeVisible();

    // Register with the same address and the newsletter box unticked.
    await openHydrated(page, '/account/register');
    const news = page.getByRole('checkbox', { name: /news and offers/ });
    await expect(news).not.toBeChecked();
    await page.getByLabel('First name').fill('Sana');
    await page.getByLabel('Last name').fill('Khan');
    await page.getByLabel('Email', { exact: true }).fill(email);
    await page.getByLabel('Mobile number').fill('03001234567');
    await page.getByLabel('Password', { exact: true }).fill(CUSTOMER_PASSWORD);
    await page.getByRole('checkbox', { name: /I agree/ }).check();
    await page.getByRole('button', { name: 'Create account' }).click();
    await expect(page).toHaveURL(/\/account(\/addresses)?$/);

    await openHydrated(page, '/account/preferences');
    await expect(page.getByRole('heading', { level: 1, name: 'Preferences' })).toBeVisible();
    const toggle = page.getByRole('checkbox', { name: 'Send me news and offers by email' });
    await expect(toggle).not.toBeChecked();

    // Turning it on in the account is an explicit subscribe, and it persists across a reload.
    await toggle.check();
    await expect(page.locator('#rp-pref-note')).toHaveText('');
    await openHydrated(page, '/account/preferences');
    await expect(toggle).toBeChecked();

    const axe = await new AxeBuilder({ page }).withTags(AXE_TAGS).analyze();
    expect(axe.violations.map((v) => v.id)).toEqual([]);

    await toggle.uncheck();
    await openHydrated(page, '/account/preferences');
    await expect(toggle).not.toBeChecked();
  });

  test('SEO-05 the preferences page is not cached or indexed, and the account API refuses a stranger', async ({ request }) => {
    expect((await request.get('/customer-api/newsletter')).status()).toBe(401);
    expect((await request.get('/customer-api/newsletter/not-a-route')).status()).toBe(404);
    const result = await request.get('/newsletter/subscribed?status=ok');
    expect(result.headers()['cache-control']).toContain('no-store');
    expect(await result.text()).toContain('noindex');
  });
});
