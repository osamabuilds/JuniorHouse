import { expect, test, type Page } from '@playwright/test';

/**
 * Storefront behaviour that a shopper feels: the newsletter Join button stays readable (also on hover), the mega-menu follows the design
 * (eyebrow column titles, Esc closes, aria-expanded), the search overlay has one close control and closes by button, Esc and navigation, and
 * pages are smooth (no layout shift, no console errors, no hydration failure).
 * Run: `STOREFRONT_BASE_URL=http://localhost:4000 npx playwright test e2e/journeys/storefront-ux`.
 */
const BASE = process.env['STOREFRONT_BASE_URL'] ?? 'http://localhost:4000';
test.use({ baseURL: BASE });

async function open(page: Page, path: string): Promise<void> {
  await page.goto(path);
  await page.waitForLoadState('networkidle');
  await page.waitForFunction(() => document.querySelector('[ngh]') === null);
}

/** WCAG relative luminance contrast of two "rgb(r, g, b)" strings. */
function contrast(a: string, b: string): number {
  const lum = (c: string): number => {
    const [r, g, bl] = (c.match(/\d+(\.\d+)?/g) ?? ['0', '0', '0']).slice(0, 3).map((v) => {
      const s = Number(v) / 255;
      return s <= 0.03928 ? s / 12.92 : ((s + 0.055) / 1.055) ** 2.4;
    }) as [number, number, number];
    return 0.2126 * r + 0.7152 * g + 0.0722 * bl;
  };
  const [hi, lo] = [lum(a), lum(b)].sort((x, y) => y - x) as [number, number];
  return (hi + 0.05) / (lo + 0.05);
}

test.describe('newsletter Join button', () => {
  test('is readable at rest, on hover and on focus (text against its own background)', async ({ page }) => {
    await open(page, '/');
    const join = page.getByRole('button', { name: 'Join' });
    await join.scrollIntoViewIfNeeded();
    const page_bg = await page.evaluate(() => getComputedStyle(document.body).backgroundColor);

    for (const state of ['rest', 'hover', 'focus'] as const) {
      if (state === 'hover') await join.hover();
      if (state === 'focus') await join.focus();
      await page.waitForTimeout(450); // the colour transition
      const { bg, fg, border } = await join.evaluate((el) => {
        const s = getComputedStyle(el);
        return { bg: s.backgroundColor, fg: s.color, border: s.borderColor };
      });
      expect(contrast(fg, bg), `${state}: text on button`).toBeGreaterThanOrEqual(4.5);
      // The button must also be visible against the page itself, by fill or by border.
      const visible = Math.max(contrast(bg, page_bg), contrast(border, page_bg));
      expect(visible, `${state}: button against the page`).toBeGreaterThanOrEqual(3);
    }
  });
});

test.describe('mega-menu', () => {
  test('opens on click with eyebrow column titles, sets aria-expanded and closes on Escape', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await open(page, '/');
    const trigger = page.getByRole('navigation', { name: 'Primary' }).getByRole('button', { name: 'Girls' });
    await expect(trigger).toHaveAttribute('aria-expanded', 'false');
    await trigger.click();
    await expect(trigger).toHaveAttribute('aria-expanded', 'true');

    const panel = page.getByRole('region', { name: 'Girls menu' });
    await expect(panel).toBeVisible();
    const title = panel.locator('.rp-navlabel').first();
    expect(await title.evaluate((e) => getComputedStyle(e).textTransform)).toBe('uppercase');
    expect(await title.evaluate((e) => parseFloat(getComputedStyle(e).fontSize))).toBeLessThanOrEqual(12);
    // Links sit flush under their title, not indented like a nested list.
    const titleX = (await title.boundingBox())!.x;
    const linkX = (await panel.locator('a').first().boundingBox())!.x;
    expect(Math.abs(linkX - titleX)).toBeLessThanOrEqual(2);

    await page.keyboard.press('Escape');
    await expect(panel).toBeHidden();
    await expect(trigger).toHaveAttribute('aria-expanded', 'false');
  });

  test('choosing a link navigates and closes the menu', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await open(page, '/');
    const nav = page.getByRole('navigation', { name: 'Primary' });
    await nav.getByRole('button', { name: 'Girls' }).click();
    await page.getByRole('region', { name: 'Girls menu' }).getByRole('link', { name: 'Dresses' }).click();
    await page.waitForURL(/dresses/);
    await expect(page.getByRole('region', { name: 'Girls menu' })).toBeHidden();
  });
});

test.describe('search overlay', () => {
  test('has one close control, closes by button and by Escape, and returns focus', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await open(page, '/');
    const field = page.locator('.rp-search input');
    await field.click();
    const dialog = page.getByRole('dialog', { name: 'Search' });
    await expect(dialog).toBeVisible();
    await page.keyboard.type('dre');

    // The browser's own clear button is hidden, so "x" exists once and is the labelled close button.
    // (getComputedStyle cannot read this pseudo-element, so look for the rule that hides it.)
    const hidden = await page.evaluate(() =>
      [...document.styleSheets].some((sheet) =>
        [...sheet.cssRules].some((r) => r instanceof CSSStyleRule && r.selectorText.includes('::-webkit-search-cancel-button') && r.style.display === 'none'),
      ),
    );
    expect(hidden).toBe(true);
    await expect(dialog.getByRole('button', { name: 'Close search' })).toHaveCount(1);

    await dialog.getByRole('button', { name: 'Close search' }).click();
    await expect(dialog).toBeHidden();

    await field.click();
    await expect(dialog).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(dialog).toBeHidden();
  });

  test('suggestions appear as you type, spaced from the chips, and Enter goes to the results page', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await open(page, '/');
    await page.locator('.rp-search input').click();
    await page.keyboard.type('dress');
    const dialog = page.getByRole('dialog', { name: 'Search' });
    const first = dialog.locator('.rp-suggestions a').first();
    await expect(first).toBeVisible();
    const chip = await dialog.locator('.rp-chip').first().boundingBox();
    const card = await dialog.locator('.rp-suggestions').boundingBox();
    expect(card!.y - (chip!.y + chip!.height)).toBeGreaterThanOrEqual(16);

    await page.keyboard.press('Enter');
    await page.waitForURL(/\/search\?q=dress/);
  });
});

test.describe('smoothness', () => {
  for (const path of ['/', '/girls', '/new-in', '/search?q=dress', '/girls/dresses/floral-summer-dress']) {
    // HYD-2: the server result is handed to the browser (ADR 0028), so hydration shows the same content instead of a loading state.
    test(`${path} has no layout shift over 0.1, no console errors and hydrates`, async ({ page }) => {
      const errors: string[] = [];
      page.on('console', (m) => {
        if (m.type() === 'error' && !/Failed to load resource/.test(m.text())) errors.push(m.text().slice(0, 160));
      });
      await page.addInitScript(() => {
        (window as unknown as { __cls: number }).__cls = 0;
        new PerformanceObserver((list) => {
          for (const e of list.getEntries() as unknown as { value: number; hadRecentInput: boolean }[]) {
            if (!e.hadRecentInput) (window as unknown as { __cls: number }).__cls += e.value;
          }
        }).observe({ type: 'layout-shift', buffered: true });
      });
      await open(page, path);
      await page.mouse.wheel(0, 1200);
      await page.waitForTimeout(800);
      const cls = await page.evaluate(() => (window as unknown as { __cls: number }).__cls);
      expect(cls).toBeLessThanOrEqual(0.1);
      expect(errors).toEqual([]);
    });
  }
});
