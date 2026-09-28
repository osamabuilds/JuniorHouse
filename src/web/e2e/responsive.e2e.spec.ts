import { Page, expect, test } from '@playwright/test';
import { PoStage, TestPo, createPo, openPo } from './support';

/**
 * Layout checks for every admin screen at phone, tablet and desktop widths: nothing may make the
 * page scroll sideways or stick out of its card, touch targets and text must be big enough to use,
 * and the heading structure must stay valid. Run against the docker stack like the other E2E specs.
 */

const VIEWPORTS = [
  { name: 'small phone 320', width: 320, height: 640 },
  { name: 'phone 375', width: 375, height: 812 },
  { name: 'large phone 414', width: 414, height: 896 },
  { name: 'tablet portrait 768', width: 768, height: 1024 },
  { name: 'tablet landscape 1024', width: 1024, height: 768 },
  { name: 'laptop 1280', width: 1280, height: 800 },
  { name: 'desktop 1920', width: 1920, height: 1080 },
];

interface Screen {
  name: string;
  open: (page: Page, pos: Record<PoStage, TestPo>) => Promise<void>;
}

const screens: Screen[] = [
  { name: 'reference data', open: async (p) => { await p.goto('/reference-data'); await expect(p.locator('table')).toBeVisible(); } },
  { name: 'styles list', open: async (p) => { await p.goto('/styles'); await expect(p.locator('table')).toBeVisible(); } },
  { name: 'style form', open: async (p) => { await p.goto('/styles'); await p.getByRole('button', { name: 'Add style' }).click(); await expect(p.locator('form')).toBeVisible(); } },
  { name: 'vendors list', open: async (p) => { await p.goto('/vendors'); await expect(p.locator('table')).toBeVisible(); } },
  { name: 'vendor form', open: async (p) => { await p.goto('/vendors'); await p.getByRole('button', { name: 'Add vendor' }).click(); await expect(p.locator('form')).toBeVisible(); } },
  { name: 'purchase orders list', open: async (p) => { await p.goto('/purchase-orders'); await expect(p.locator('table')).toBeVisible(); } },
  { name: 'raise PO form', open: async (p) => { await p.goto('/purchase-orders'); await p.getByRole('button', { name: 'Raise PO' }).click(); await expect(p.locator('form.po-form')).toBeVisible(); } },
  { name: 'draft PO', open: async (p, pos) => { await openPo(p, pos.draft); } },
  { name: 'draft PO send confirmation', open: async (p, pos) => { await openPo(p, pos.draft); await p.getByRole('button', { name: 'Send to Vendor' }).click(); await expect(p.getByRole('alertdialog')).toBeVisible(); } },
  { name: 'sent PO', open: async (p, pos) => { await openPo(p, pos.sent); } },
  { name: 'vendor response form', open: async (p, pos) => { await openPo(p, pos.sent); await p.getByRole('button', { name: 'Record vendor response' }).click(); await expect(p.locator('app-vendor-response-form')).toBeVisible(); } },
  { name: 'counter-proposal form', open: async (p, pos) => { await openPo(p, pos.sent); await p.getByRole('button', { name: 'Record vendor response' }).click(); await p.locator('#vresp-outcome').selectOption({ label: 'Countered with different terms' }); await expect(p.locator('app-amend-form')).toBeVisible(); } },
  { name: 'acknowledged PO', open: async (p, pos) => { await openPo(p, pos.acknowledged); } },
  { name: 'amendment form', open: async (p, pos) => { await openPo(p, pos.acknowledged); await p.getByRole('button', { name: 'Amend', exact: true }).click(); await expect(p.locator('app-amend-form')).toBeVisible(); } },
  { name: 'PO with a pending amendment', open: async (p, pos) => { await openPo(p, pos.pending); await expect(p.getByRole('heading', { name: /awaits a decision/ })).toBeVisible(); } },
  { name: 'vendor view', open: async (p, pos) => { await p.goto(`/purchase-orders/${pos.acknowledged.id}/vendor-view`); await expect(p.locator('article.vv h1')).toBeVisible(); } },
];

for (const viewport of VIEWPORTS) {
  test.describe(`at ${viewport.name}px`, () => {
    test.use({ viewport: { width: viewport.width, height: viewport.height } });
    const isPhone = viewport.width < 640;

    let pos: Record<PoStage | 'pending', TestPo>;
    test.beforeAll(async ({ request }) => {
      pos = {
        draft: await createPo(request, 'draft'),
        sent: await createPo(request, 'sent'),
        acknowledged: await createPo(request, 'acknowledged'),
        pending: await createPo(request, 'acknowledged'),
      };
      // Give one PO an open, buyer-proposed revision so that panel is covered too.
      const api = process.env['API_BASE_URL'] ?? 'http://localhost:8080';
      const current = await (await request.get(`${api}/api/purchase-orders/${pos.pending.id}`)).json();
      await request.post(`${api}/api/purchase-orders/${pos.pending.id}/amendments`, {
        data: {
          initiatorId: 1,
          reasonId: 1,
          impactNote: 'E2E layout check',
          vendorMessage: null,
          unitCost: 480,
          expectedDeliveryDate: current.expectedDeliveryDate,
          latestAcceptableDate: current.latestAcceptableDate,
          overTolerancePercent: current.overTolerancePercent,
          underTolerancePercent: current.underTolerancePercent,
          paymentTermId: current.paymentTermId,
          advancePercent: current.advancePercent,
          fabricResponsibilityId: current.fabricResponsibilityId,
          lines: current.lines,
        },
      });
    });

    for (const screen of screens) {
      test(`${screen.name}: fits the screen, is usable and well structured`, async ({ page }) => {
        await screen.open(page, pos);
        await page.waitForTimeout(250); // let lookups fill in names

        const report = await page.evaluate((phone) => {
          const doc = document.documentElement;
          const visible = (el: Element): boolean => {
            const box = (el as HTMLElement).getBoundingClientRect();
            const style = getComputedStyle(el);
            return box.width > 0 && box.height > 0 && style.visibility !== 'hidden' && style.display !== 'none';
          };
          const insideScroller = (el: Element): boolean => {
            for (let a = el.parentElement; a; a = a.parentElement) {
              const overflowX = getComputedStyle(a).overflowX;
              // A 1px-wide clipped box is how a visually hidden element (a phone table's header row) is hidden.
              if ((a as HTMLElement).getBoundingClientRect().width <= 1) return true;
              if (overflowX === 'auto' || overflowX === 'scroll') return true;
            }
            return false;
          };

          const sticksOut: string[] = [];
          for (const el of document.querySelectorAll('body *')) {
            if (!visible(el) || insideScroller(el)) continue;
            const box = (el as HTMLElement).getBoundingClientRect();
            if (box.right > doc.clientWidth + 1 || box.left < -1) {
              sticksOut.push(`${el.tagName.toLowerCase()}.${(el as HTMLElement).className || ''}`.slice(0, 60));
            }
          }

          const small: string[] = [];
          for (const el of document.querySelectorAll('button, a.button, select, input:not([type=checkbox]):not([type=radio]):not([type=file]), textarea')) {
            if (!visible(el) || insideScroller(el)) continue;
            const box = (el as HTMLElement).getBoundingClientRect();
            const isSmallButton = el.classList.contains('button--small');
            const minHeight = phone ? (isSmallButton ? 34 : 42) : 30;
            if (box.height < minHeight) small.push(`${el.tagName.toLowerCase()} "${(el.textContent || (el as HTMLInputElement).id || '').trim().slice(0, 20)}" ${Math.round(box.height)}px`);
          }

          const smallFont: string[] = [];
          if (phone) {
            for (const el of document.querySelectorAll('input:not([type=checkbox]):not([type=radio]):not([type=file]), select, textarea')) {
              if (visible(el) && parseFloat(getComputedStyle(el).fontSize) < 16) smallFont.push((el as HTMLElement).id || el.tagName);
            }
          }

          const levels = Array.from(document.querySelectorAll('h1, h2, h3, h4')).filter(visible).map((h) => +h.tagName[1]);
          const skipped = levels.some((level, i) => i > 0 && level - levels[i - 1] > 1);

          return {
            pageScroll: doc.scrollWidth - doc.clientWidth,
            sticksOut: [...new Set(sticksOut)].slice(0, 5),
            small: small.slice(0, 5),
            smallFont: smallFont.slice(0, 5),
            h1Count: levels.filter((l) => l === 1).length,
            mainCount: document.querySelectorAll('main').length,
            skipped,
            levels: levels.join(','),
            bodyFont: parseFloat(getComputedStyle(document.body).fontSize),
          };
        }, isPhone);

        expect(report.pageScroll, 'page must not scroll sideways').toBeLessThanOrEqual(1);
        expect(report.sticksOut, 'nothing may stick out of the screen').toEqual([]);
        expect(report.small, 'controls must be big enough to tap').toEqual([]);
        expect(report.smallFont, 'phone inputs must be 16px+ so the browser does not zoom').toEqual([]);
        expect(report.h1Count, 'exactly one h1').toBe(1);
        expect(report.mainCount, 'exactly one main landmark').toBe(1);
        expect(report.skipped, `headings must not skip a level (${report.levels})`).toBe(false);
        expect(report.bodyFont, 'body text is at least 16px').toBeGreaterThanOrEqual(16);
      });
    }
  });
}

test.describe('phone specifics', () => {
  test.use({ viewport: { width: 375, height: 812 } });

  test('PO list rows turn into labelled cards, and each card is fully on screen', async ({ page, request }) => {
    await createPo(request, 'sent');
    await page.goto('/purchase-orders');
    await expect(page.locator('table.responsive tbody tr').first()).toBeVisible();

    const firstCard = page.locator('table.responsive tbody tr').first();
    await expect(firstCard.locator('td').first()).toHaveAttribute('data-label', 'PO number');
    const box = await firstCard.boundingBox();
    expect(box!.x).toBeGreaterThanOrEqual(0);
    expect(box!.x + box!.width).toBeLessThanOrEqual(375);
  });

  test('the navigation stays reachable: all four links are visible without scrolling sideways', async ({ page }) => {
    await page.goto('/vendors');

    for (const name of ['Reference Data', 'Styles', 'Vendors', 'Purchase Orders']) {
      const link = page.getByRole('navigation').getByRole('link', { name });
      await expect(link).toBeVisible();
      const box = await link.boundingBox();
      expect(box!.x + box!.width).toBeLessThanOrEqual(375);
    }
  });

  test('PO action buttons wrap inside the card instead of spilling out', async ({ page, request }) => {
    const po = await createPo(request, 'sent');
    await openPo(page, po);

    const card = await page.locator('.po-detail').boundingBox();
    for (const button of await page.locator('.po-detail__actions .button').all()) {
      const box = await button.boundingBox();
      expect(box!.x + box!.width).toBeLessThanOrEqual(card!.x + card!.width + 1);
    }
  });
});

test.describe('print', () => {
  test('the vendor view prints on A4 without the admin chrome', async ({ page, request }) => {
    const po = await createPo(request, 'acknowledged');
    await page.goto(`/purchase-orders/${po.id}/vendor-view`);
    await expect(page.locator('article.vv h1')).toBeVisible();

    await page.emulateMedia({ media: 'print' });

    await expect(page.locator('header')).toHaveCount(1); // the article's own heading block, not the admin header
    await expect(page.locator('nav')).toHaveCount(0);
    const pdf = await page.pdf({ format: 'A4' });
    expect(pdf.length).toBeGreaterThan(1000);
  });
});
