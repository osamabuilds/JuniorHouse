import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';

/**
 * SCRUM-205 site chrome (header, mega-menu, mobile menu, search overlay, footer). Runs against the storefront
 * container (`docker compose up -d --no-deps storefront`). Until Story 3 adds the home page, the chrome is exercised on
 * the development-only /_foundations page.
 */
const BASE = process.env['STOREFRONT_BASE_URL'] ?? 'http://localhost:4000';
const PAGE = '/_foundations';
const AXE_TAGS = ['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'];
const summary = (r: { violations: { id: string; nodes: { target: unknown }[] }[] }) =>
  r.violations.map((v) => `${v.id}: ${v.nodes.map((n) => JSON.stringify(n.target)).join(', ')}`);

const VIEWPORTS = [
  { name: 'phone', width: 390, height: 844, desktop: false },
  { name: 'tablet', width: 768, height: 1024, desktop: false },
  { name: 'desktop', width: 1280, height: 900, desktop: true },
];

test.describe('chrome in the server HTML (no scripting)', () => {
  test.use({ baseURL: BASE });

  test('SC-1 SC-2 SC-3a the server returns the landmarks, the five entries and the panel links', async ({ request }) => {
    const html = await (await request.get(PAGE)).text();
    expect(html.match(/<header\b/g)?.length).toBe(1);
    expect(html.match(/<main\b/g)?.length).toBe(1);
    expect(html.match(/<footer\b/g)?.length).toBe(1);
    expect(html).toContain('aria-label="Primary"');
    for (const label of ['Girls', 'Boys', 'New in', 'Collections', 'Sale']) expect(html).toContain(`>${label}</`);
    // Members and further members of Boys are present although the panel is closed.
    expect(html).toContain('href="/boys/tees-and-shirts"');
    expect(html).toContain('href="/boys/tees-and-shirts/polo-shirts"');
    expect(html).toContain('href="/boys/casual-shirts"');
    expect(html).toContain('href="/boys?age=4y"');
  });

  test('SC-10 the header search is a plain GET form to /search', async ({ request }) => {
    const html = await (await request.get(PAGE)).text();
    expect(html).toMatch(/<form[^>]*class="rp-search"[^>]*action="\/search"[^>]*method="get"|<form[^>]*action="\/search"[^>]*method="get"[^>]*class="rp-search"/);
  });

  test('SC-11 the footer links to the help and brand pages, shows WhatsApp and has no newsletter form', async ({ request }) => {
    const html = await (await request.get(PAGE)).text();
    for (const href of ['/delivery', '/exchanges', '/size-guide', '/contact', '/our-story', '/made-in-pakistan']) expect(html).toContain(`href="${href}"`);
    expect(html).toContain('href="https://wa.me/923315613217"');
    const footer = html.slice(html.indexOf('<footer'), html.indexOf('</footer>'));
    expect(footer).not.toMatch(/type="email"|<form/);
  });
});

for (const vp of VIEWPORTS) {
  test.describe(`chrome at ${vp.name} (${vp.width}px)`, () => {
    test.use({ baseURL: BASE, viewport: { width: vp.width, height: vp.height } });

    test('SC-16 SC-19 no horizontal scroll, no axe violations, every chrome control is at least 44px', async ({ page }) => {
      await page.goto(PAGE);
      const results = await new AxeBuilder({ page }).withTags(AXE_TAGS).analyze();
      expect(summary(results)).toEqual([]);
      const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
      expect(overflow).toBeLessThanOrEqual(0);
      const small = await page.$$eval('header :is(button, input, a), footer :is(button, input, a)', (els) =>
        els
          .filter((e) => (e as HTMLElement).offsetParent !== null)
          .filter((e) => e.getBoundingClientRect().height < 44)
          .map((e) => e.outerHTML.slice(0, 90)),
      );
      expect(small).toEqual([]);
    });

    if (vp.desktop) {
      test('SC-3a SC-6 a click opens the Boys panel, Escape closes it and returns focus; hover does not open it', async ({ page }) => {
        await page.goto(PAGE);
        const boys = page.getByRole('button', { name: 'Boys' });
        await boys.hover();
        await expect(boys).toHaveAttribute('aria-expanded', 'false');
        await boys.click();
        await expect(boys).toHaveAttribute('aria-expanded', 'true');
        await expect(page.getByRole('link', { name: 'Casual shirts' })).toBeVisible();
        await expect(page.getByRole('link', { name: 'Polo shirts' })).toBeVisible();
        const open = await new AxeBuilder({ page }).withTags(AXE_TAGS).analyze();
        expect(summary(open)).toEqual([]);
        await page.keyboard.press('Escape');
        await expect(boys).toHaveAttribute('aria-expanded', 'false');
        await expect(boys).toBeFocused();
      });

      test('SC-5 the Boys panel offers one link per age with the age code', async ({ page }) => {
        await page.goto(PAGE);
        await page.getByRole('button', { name: 'Boys' }).click();
        const ages = await page.locator('#mega-boys .rp-mega__ages a').allTextContents();
        expect(ages.map((a) => a.trim())).toEqual(['1Y', '2Y', '3Y', '4Y', '5Y', '6Y', '7Y', '8Y']);
        await expect(page.locator('#mega-boys .rp-mega__ages a').nth(3)).toHaveAttribute('href', '/boys?age=4y');
      });
    } else {
      test('SC-7 SC-8 the menu button opens a dialog with nested accordions; Escape closes it and returns focus', async ({ page }) => {
        await page.goto(PAGE);
        const opener = page.getByRole('button', { name: 'Open menu' });
        await opener.click();
        const dialog = page.getByRole('dialog', { name: 'Menu' });
        await expect(dialog).toBeVisible();
        await dialog.getByText('Boys', { exact: true }).click();
        await dialog.getByText('Tees and shirts', { exact: true }).click();
        await expect(dialog.getByRole('link', { name: 'Polo shirts' })).toBeVisible();
        const open = await new AxeBuilder({ page }).withTags(AXE_TAGS).analyze();
        expect(summary(open)).toEqual([]);
        await page.keyboard.press('Escape');
        await expect(dialog).toBeHidden();
        await expect(opener).toBeFocused();
      });
    }

    test('SC-9 SC-10 the search overlay shows suggestions after typing and Enter goes to /search?q=', async ({ page }) => {
      await page.goto(PAGE);
      await page.getByRole('button', { name: 'Search' }).or(page.getByRole('searchbox', { name: 'Search' }).first()).first().click();
      const dialog = page.getByRole('dialog', { name: 'Search' });
      await expect(dialog).toBeVisible();
      const open = await new AxeBuilder({ page }).withTags(AXE_TAGS).analyze();
      expect(summary(open)).toEqual([]);
      const field = dialog.getByRole('searchbox', { name: 'Search' });
      await field.fill('shirt');
      await expect(dialog.getByRole('heading', { name: 'Suggestions' })).toBeVisible();
      expect(await dialog.locator('.rp-suggestions li').count()).toBeLessThanOrEqual(4);
      await field.press('Enter');
      await expect(page).toHaveURL(/\/search\?q=shirt/);
    });

    test('SC-9 markup typed into search is shown as text', async ({ page }) => {
      await page.goto(PAGE);
      await page.getByRole('button', { name: 'Search' }).or(page.getByRole('searchbox', { name: 'Search' }).first()).first().click();
      const dialog = page.getByRole('dialog', { name: 'Search' });
      await dialog.getByRole('searchbox', { name: 'Search' }).fill('<img src=x onerror=alert(1)>');
      await expect(dialog.getByText('Nothing for')).toBeVisible();
      expect(await dialog.locator('img[src="x"]').count()).toBe(0);
    });
  });
}

test.describe('chrome accessibility details', () => {
  test.use({ baseURL: BASE, viewport: { width: 1280, height: 900 } });

  test('SC-1 the skip link is the first tab stop and moves focus to main', async ({ page }) => {
    await page.goto(PAGE);
    await page.keyboard.press('Tab');
    const skip = page.getByRole('link', { name: 'Skip to content' });
    await expect(skip).toBeFocused();
    await page.keyboard.press('Enter');
    await expect(page.locator('main')).toBeFocused();
  });

  test('SC-18 reduced motion removes the dialog animation', async ({ page }) => {
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto(PAGE);
    await page.getByRole('button', { name: 'Open menu' }).click();
    const name = await page.locator('dialog[open].rp-sheet').evaluate((e) => getComputedStyle(e).animationName);
    expect(name).toBe('none');
  });
});
