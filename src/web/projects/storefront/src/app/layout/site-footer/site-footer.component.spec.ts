import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Store, provideStore } from '@ngrx/store';
import { SiteSettingsActions, provideSiteSettings } from '../../features/storefront/site-settings';
import { launchSettings } from '../../shared/data/launch-settings';
import { SiteSettings } from '../../shared/models/site-settings.model';
import { SiteFooterComponent } from './site-footer.component';

function render(settings: SiteSettings = launchSettings): ComponentFixture<SiteFooterComponent> {
  TestBed.configureTestingModule({ providers: [provideRouter([{ path: '**', children: [] }]), provideStore({}), provideSiteSettings()] });
  TestBed.inject(Store).dispatch(SiteSettingsActions.loadSuccess({ settings, source: 'api' }));
  const f = TestBed.createComponent(SiteFooterComponent);
  f.detectChanges();
  return f;
}
const el = (f: ComponentFixture<unknown>) => f.nativeElement as HTMLElement;

describe('SiteFooterComponent', () => {
  it('SC-11 is a dark footer landmark with link columns as lists, from the settings', () => {
    const f = render();
    const root = el(f).querySelector('footer')!;
    expect(root.classList.contains('rp-dark')).toBe(true);
    const titles = [...root.querySelectorAll('h2')].map((h) => h.textContent!.trim());
    expect(titles).toEqual(['Shop', 'Help', 'Romp']);
    expect(root.querySelectorAll('nav ul').length).toBe(3);
    const hrefs = [...root.querySelectorAll('nav a')].map((a) => a.getAttribute('href'));
    expect(hrefs).toEqual(expect.arrayContaining(['/new-in', '/delivery', '/exchanges', '/size-guide', '/contact', '/our-story', '/made-in-pakistan']));
  });

  it('SC-11 O-7 shows a WhatsApp link built from the brand number', () => {
    const f = render();
    const wa = [...el(f).querySelectorAll('a')].find((a) => a.getAttribute('href') === 'https://wa.me/923315613217');
    expect(wa).toBeTruthy();
    expect(el(f).textContent).toContain('+92 331 5613217');
  });

  it('O-7 a malformed WhatsApp number shows no link rather than a broken one', () => {
    const f = render({ ...launchSettings, brand: { ...launchSettings.brand, whatsapp: '03315613217 ; drop' } });
    expect(el(f).querySelector('a[href^="https://wa.me"]:not([data-footer-link])')).toBeNull();
  });

  it('O-5 has no newsletter form', () => {
    const f = render();
    expect(el(f).querySelector('form')).toBeNull();
    expect(el(f).querySelector('input[type="email"]')).toBeNull();
  });

  it('SC-11 the oversized wordmark is decorative and hidden from assistive technology', () => {
    const big = el(render()).querySelector('.rp-footer__big')!;
    expect(big.getAttribute('aria-hidden')).toBe('true');
  });

  it('SC-3 SC-12 follows the settings: a new link appears, a link to an unpublished page disappears', () => {
    const edited: SiteSettings = {
      ...launchSettings,
      pages: launchSettings.pages.map((p) => (p.slug === 'terms' ? { ...p, status: 'draft' as const } : p)),
      footer: launchSettings.footer.map((c) => (c.title === 'Shop' ? { ...c, links: [...c.links, { label: 'Gift cards', target: { kind: 'path' as const, path: '/sale' }, enabled: true }] } : c)),
    };
    const text = el(render(edited)).textContent!;
    expect(text).toContain('Gift cards');
    expect(text).not.toContain('Terms');
  });

  it('shows the brand name in the legal row', () => {
    expect(el(render()).querySelector('.rp-footer__legal')!.textContent).toContain('Romp');
  });
});
