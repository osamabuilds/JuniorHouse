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

  it('ST-8 the catch-all route (no slug) is a 404, never a blank 200', () => {
    const { init, el } = render({ notFoundRoute: true });
    expect(init.status).toBe(404);
    expect(el.querySelector('h1')).not.toBeNull();
  });
});
