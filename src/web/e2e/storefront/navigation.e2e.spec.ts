import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';

/**
 * Navigation (SCRUM-209, NAV-1..10) against the storefront container and the API behind it: the menu built from lookups, the keyboard
 * and phone behaviour, the home page. Run with `STOREFRONT_BASE_URL=http://localhost:4000 npx playwright test e2e/storefront/navigation`.
 */
const BASE = process.env['STOREFRONT_BASE_URL'] ?? 'http://localhost:4000';
const AXE_TAGS = ['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'];
const summary = (r: { violations: { id: string; nodes: { target: unknown }[] }[] }) =>
  r.violations.map((v) => `${v.id}: ${v.nodes.map((n) => JSON.stringify(n.target)).join(', ')}`);

interface Menu { audiences: { code: string; name: string; path: string; entries: { kind: string; label: string; path: string }[] }[]; hasNewIn: boolean; hasSale: boolean }

test.use({ baseURL: BASE });

test.describe('the menu in the server HTML (no scripting)', () => {
  test('NAV-2 NAV-7 every menu entry the API returns is a link in the HTML the server sends', async ({ request }) => {
    const menu = (await (await request.get('/api/storefront/menu')).json()) as Menu;
    const html = await (await request.get('/')).text();
    expect(html.match(/<header\b/g)?.length).toBe(1);
    expect(html.match(/<main\b/g)?.length).toBe(1);
    for (const audience of menu.audiences) {
      expect(html, audience.name).toContain(`>${audience.name}</`);
      for (const entry of audience.entries) expect(html, entry.path).toContain(`href="${entry.path}"`);
    }
    if (menu.hasNewIn) expect(html).toContain('href="/new-in"');
    if (menu.hasSale) expect(html).toContain('href="/sale"');
  });

  test('NAV-8 an age entry links to the audience page with the age code in the query', async ({ request }) => {
    const menu = (await (await request.get('/api/storefront/menu')).json()) as Menu;
    const age = menu.audiences.flatMap((a) => a.entries).find((e) => e.kind === 'age');
    test.skip(!age, 'no age entries in this shop');
    expect(age!.path).toMatch(/^\/[a-z0-9-]+\?age=[a-z0-9-]+$/);
  });
});

test.describe('keyboard and phone', () => {
  test('NAV-2 an audience opens on Enter, Escape closes it and returns focus to the button', async ({ page, request }) => {
    const menu = (await (await request.get('/api/storefront/menu')).json()) as Menu;
    test.skip(menu.audiences.length === 0, 'no audiences yet');
    await page.setViewportSize({ width: 1280, height: 900 });
    await page.goto('/');
    const button = page.locator('.rp-nav button').first();
    await button.focus();
    await page.keyboard.press('Enter');
    await expect(button).toHaveAttribute('aria-expanded', 'true');
    await page.keyboard.press('Escape');
    await expect(button).toHaveAttribute('aria-expanded', 'false');
    await expect(button).toBeFocused();
  });

  test('NAV-6 the phone menu opens, shows the links and closes, with targets of at least 44 px at 320 px', async ({ page }) => {
    await page.setViewportSize({ width: 320, height: 700 });
    await page.goto('/');
    await page.getByRole('button', { name: 'Open menu' }).click();
    const dialog = page.getByRole('dialog', { name: 'Menu' });
    await expect(dialog).toBeVisible();
    const small = await dialog.locator('a, summary, button').evaluateAll((els) =>
      els.filter((e) => (e as HTMLElement).offsetParent !== null && e.getBoundingClientRect().height < 44).map((e) => e.outerHTML.slice(0, 80)));
    expect(small).toEqual([]);
    await page.keyboard.press('Escape');
    await expect(dialog).toBeHidden();
  });
});

test.describe('the home page', () => {
  test('NAV-10 one h1, Organization and WebSite structured data, status 200', async ({ page }) => {
    const response = await page.goto('/');
    expect(response!.status()).toBe(200);
    await expect(page.locator('h1')).toHaveCount(1);
    const types = await page.locator('script[type="application/ld+json"][data-rp="site"]').evaluateAll((els) => els.map((e) => JSON.parse(e.textContent!)['@type']));
    expect(types).toEqual(['Organization', 'WebSite']);
  });

  test('NAV-10 the new-in tiles come from the API: the same products, newest first', async ({ page, request }) => {
    const home = (await (await request.get('/api/storefront/home')).json()) as { newArrivals: { name: string }[] };
    await page.goto('/');
    const names = await page.locator('#new-h').locator('xpath=ancestor::section').locator('.rp-card__name').allTextContents();
    expect(names.map((n) => n.trim())).toEqual(home.newArrivals.map((t) => t.name));
  });

  for (const width of [390, 1280]) {
    test(`NFR-19 the home page has no axe violations at ${width}px`, async ({ page }) => {
      await page.setViewportSize({ width, height: 900 });
      await page.goto('/');
      const results = await new AxeBuilder({ page }).withTags(AXE_TAGS).analyze();
      expect(summary(results)).toEqual([]);
    });
  }
});
