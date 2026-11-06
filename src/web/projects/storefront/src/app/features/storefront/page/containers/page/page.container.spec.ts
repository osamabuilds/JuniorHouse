import { RESPONSE_INIT } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { provideEffects } from '@ngrx/effects';
import { Store, provideStore } from '@ngrx/store';
import { BehaviorSubject } from 'rxjs';
import { launchSettings } from '../../../../../shared/data/launch-settings';
import { SeoService } from '../../../../../shared/seo/seo.service';
import { SiteSettingsActions, provideSiteSettings } from '../../../site-settings';
import { providePage } from '../../page.providers';
import { PageActions } from '../../store/page.actions';
import { PageContainer } from './page.container';
import { vi } from 'vitest';

function render(opts: { slug?: string; notFoundRoute?: boolean; settings?: typeof launchSettings }) {
  const seo = { set: vi.fn() };
  const init: ResponseInit = { status: 200 };
  const route = { paramMap: new BehaviorSubject(convertToParamMap(opts.slug ? { slug: opts.slug } : {})), snapshot: { data: opts.notFoundRoute ? { notFound: true } : {} } };
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]), provideStore({}), provideEffects(), provideSiteSettings(), providePage(),
      { provide: ActivatedRoute, useValue: route },
      { provide: SeoService, useValue: seo },
      { provide: RESPONSE_INIT, useValue: init },
    ],
  });
  TestBed.inject(Store).dispatch(SiteSettingsActions.loadSuccess({ settings: opts.settings ?? launchSettings, source: 'launch' }));
  const f: ComponentFixture<PageContainer> = TestBed.createComponent(PageContainer);
  f.detectChanges();
  return { f, el: f.nativeElement as HTMLElement, seo, init };
}

describe('PageContainer', () => {
  it('SC-13 renders one h1, a breadcrumb whose last item is not a link, and the page blocks', () => {
    const { el } = render({ slug: 'size-guide' });
    expect(el.querySelectorAll('h1').length).toBe(1);
    expect(el.querySelector('h1')!.textContent).toBe('Size guide');
    const crumbs = el.querySelectorAll('nav[aria-label="Breadcrumb"] ol li');
    expect(crumbs.length).toBe(2);
    expect(crumbs[0].querySelector('a')!.getAttribute('href')).toBe('/');
    expect(crumbs[1].querySelector('a')).toBeNull();
    expect(crumbs[1].querySelector('[aria-current="page"]')).not.toBeNull();
    expect(el.querySelectorAll('ol li').length).toBeGreaterThan(2);
    expect(el.querySelector('table')).toBeNull();
  });

  it('PR-13 a content page has the Help eyebrow, a Help menu beside the article with the current page marked, and a prose article', () => {
    const { el } = render({ slug: 'delivery' });
    const menu = el.querySelector('nav.rp-sidenav[aria-label="Help"]')!;

    expect(el.querySelector('.rp-eyebrow')!.textContent).toContain('Help');
    expect(menu.querySelector('a[aria-current="page"]')!.getAttribute('href')).toBe('/delivery');
    expect(menu.querySelector('a[href="/exchanges"]')).not.toBeNull();
    expect(menu.querySelector('a[href="/privacy"]')).toBeNull();
    expect(el.querySelector('article.rp-prose')).not.toBeNull();
    expect(el.querySelectorAll('main, h1').length).toBe(1);
  });

  it('SC-13 headings never skip a level', () => {
    const { el } = render({ slug: 'size-guide' });
    const levels = [...el.querySelectorAll('h1,h2,h3')].map((h) => Number(h.tagName[1]));
    levels.reduce((prev, cur) => { expect(cur - prev).toBeLessThanOrEqual(1); return cur; }, 0);
  });

  it('SC-13 sets the unique title, description, canonical path and the breadcrumb trail through the SEO service', () => {
    const { seo } = render({ slug: 'delivery' });
    expect(seo.set).toHaveBeenCalledWith(expect.objectContaining({
      title: 'Delivery',
      path: '/delivery',
      siteName: 'Romp',
      crumbs: [{ name: 'Home', path: '/' }, { name: 'Delivery', path: '/delivery' }],
    }));
    expect((seo.set.mock.calls[0][0] as { noindex?: boolean }).noindex).toBeFalsy();
  });

  it('SC-14 the contact page shows the WhatsApp fact from the settings', () => {
    const { el } = render({ slug: 'contact' });
    expect(el.querySelector('a[href="https://wa.me/923315613217"]')).not.toBeNull();
  });

  it('ST-8 an unknown slug returns status 404, is marked noindex and still offers a way on', () => {
    const { el, seo, init } = render({ slug: 'nope' });
    expect(init.status).toBe(404);
    expect(el.querySelector('h1')!.textContent).toContain('could not find');
    expect(el.querySelectorAll('a[href="/new-in"]').length).toBeGreaterThan(0);
    expect((seo.set.mock.calls.at(-1)![0] as { noindex?: boolean }).noindex).toBe(true);
  });

  it('PR-10 an unknown page shows the decorative 404 and the way home; PR-12 nothing here is indexable', () => {
    const { el, seo } = render({ slug: 'nope' });

    expect(el.querySelector('.rp-notfound__code')!.getAttribute('aria-hidden')).toBe('true');
    expect(el.querySelector('a[href="/"]')!.textContent).toContain('Back to home');
    expect((seo.set.mock.calls.at(-1)![0] as { noindex?: boolean }).noindex).toBe(true);
  });

  it('ST-8 a draft page returns 404 and an archived page returns 410', () => {
    expect(render({ slug: 'privacy' }).init.status).toBe(404);
    TestBed.resetTestingModule();
    const settings = { ...launchSettings, pages: launchSettings.pages.map((p) => (p.slug === 'contact' ? { ...p, status: 'archived' as const } : p)) };
    expect(render({ slug: 'contact', settings }).init.status).toBe(410);
  });

  it('PR-14 the facts on the content pages follow the settings: change the value and the page changes, nothing is typed into a template', () => {
    const changed = {
      ...launchSettings,
      brand: { ...launchSettings.brand, whatsapp: '923001112223', email: 'changed@example.com', hours: 'Sundays only' },
      exchange: { windowDays: 11, conditions: ['Condition from settings'] },
      delivery: [{ city: 'Quetta', minDays: 4, maxDays: 6, charge: 777, courier: 'Courier Q', enabled: true }],
    } as typeof launchSettings;

    const contact = render({ slug: 'contact', settings: changed }).el;
    expect(contact.querySelector('a[href="https://wa.me/923001112223"]')).not.toBeNull();
    expect(contact.querySelector('a[href="https://wa.me/923315613217"]')).toBeNull();
    expect(contact.textContent).toContain('changed@example.com');
    expect(contact.textContent).toContain('Sundays only');
    TestBed.resetTestingModule();

    const exchanges = render({ slug: 'exchanges', settings: changed }).el.textContent;
    expect(exchanges).toContain('11 days');
    expect(exchanges).toContain('Condition from settings');
    TestBed.resetTestingModule();

    const delivery = render({ slug: 'delivery', settings: changed }).el.textContent!;
    expect(delivery).toContain('Quetta');
    expect(delivery).toContain('Rs. 777');
    expect(delivery).toContain('Courier Q');
  });

  it('PR-14 with no fact entered in the settings the pages show nothing for it, not a typed-in default', () => {
    const empty = { ...launchSettings, brand: { name: 'Romp', tagline: '', whatsapp: '' }, delivery: [], exchange: { windowDays: 0, conditions: [] } } as typeof launchSettings;

    const contact = render({ slug: 'contact', settings: empty }).el;
    expect(contact.querySelector('a[href^="https://wa.me/"]')).toBeNull();
    expect(contact.textContent).not.toContain('923315613217');
    TestBed.resetTestingModule();
    expect(render({ slug: 'exchanges', settings: empty }).el.textContent).not.toMatch(/\d+ days/);
  });

  it('SC-15 a size guide table lists every configured age, from the settings, with column headers', () => {
    const ages = launchSettings.ages.filter((a) => a.enabled);
    const sizeGuide = {
      slug: 'size-guide', title: 'Size guide', description: 'How to choose.', status: 'published' as const,
      blocks: [{ type: 'table' as const, caption: 'Measurements', headers: ['Age', 'Chest (cm)', 'Height (cm)'], rows: ages.map((a, i) => [a.label, `${50 + i * 2}`, `${90 + i * 6}`]) }],
    };
    const { f, el } = render({ slug: 'size-guide' });
    // In production the published page comes from the API; hand the store that version.
    TestBed.inject(Store).dispatch(PageActions.loaded({ slug: 'size-guide', result: { status: 'ok', page: sizeGuide } }));
    f.detectChanges();
    const table = el.querySelector('table')!;

    expect(table).not.toBeNull();
    expect(table.querySelector('caption')!.textContent).toBe('Measurements');
    expect([...table.querySelectorAll('thead th')].map((h) => h.getAttribute('scope'))).toEqual(['col', 'col', 'col']);
    expect([...table.querySelectorAll('tbody tr')].map((r) => r.querySelector('td')!.textContent)).toEqual(ages.map((a) => a.label));
  });

  it('ST-8 the catch-all route (no slug) is a 404, never a blank 200', () => {
    const { init, el } = render({ notFoundRoute: true });
    expect(init.status).toBe(404);
    expect(el.querySelector('h1')).not.toBeNull();
  });
});
