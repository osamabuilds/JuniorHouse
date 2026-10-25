import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideStore } from '@ngrx/store';
import { NavigationActions, provideNavigation } from '../../features/storefront/navigation';
import { sampleMenu } from '../../features/storefront/navigation/testing/sample-menu';
import { Menu } from '../../features/storefront/navigation/models/menu.model';
import { provideSiteSettings } from '../../features/storefront/site-settings';
import { SiteSettingsActions } from '../../features/storefront/site-settings';
import { launchSettings } from '../../shared/data/launch-settings';
import { SiteHeaderComponent } from './site-header.component';
import { Store } from '@ngrx/store';

function render(menu: Menu = sampleMenu): ComponentFixture<SiteHeaderComponent> {
  TestBed.configureTestingModule({
    providers: [provideRouter([{ path: '**', children: [] }]), provideStore({}), provideSiteSettings(), provideNavigation()],
  });
  const store = TestBed.inject(Store);
  store.dispatch(SiteSettingsActions.loadSuccess({ settings: launchSettings, source: 'api' }));
  store.dispatch(NavigationActions.loadMenuSuccess({ menu }));
  const f = TestBed.createComponent(SiteHeaderComponent);
  f.detectChanges();
  return f;
}
const q = (f: ComponentFixture<unknown>, sel: string) => (f.nativeElement as HTMLElement).querySelector<HTMLElement>(sel)!;
const qa = (f: ComponentFixture<unknown>, sel: string) => [...(f.nativeElement as HTMLElement).querySelectorAll<HTMLElement>(sel)];

describe('SiteHeaderComponent', () => {
  it('SC-1 renders a header with a primary nav listing the enabled entries in order', () => {
    const f = render();
    expect(q(f, 'header')).not.toBeNull();
    expect(q(f, 'nav[aria-label="Primary"]')).not.toBeNull();
    expect(qa(f, '.rp-nav > li').map((li) => li.textContent!.trim())).toEqual(['Girls', 'Boys', 'New in', 'Collections', 'Sale']);
  });

  it('SC-1 the wordmark link has an accessible name from the brand setting', () => {
    const f = render();
    expect(q(f, '.rp-logo').getAttribute('aria-label')).toBe('Romp, home');
  });

  it('SC-2 an entry with members is a button, an entry without members is a link', () => {
    const f = render();
    const girls = qa(f, '.rp-nav > li')[0].querySelector('button')!;
    expect(girls.getAttribute('aria-expanded')).toBe('false');
    expect(girls.getAttribute('aria-haspopup')).toBe('true');
    expect(qa(f, '.rp-nav > li')[2].querySelector('a')!.getAttribute('href')).toBe('/new-in');
  });

  it('SC-2 the panel links are in the DOM while closed, for crawlers, but hidden', () => {
    const f = render();
    const panel = q(f, '#mega-boys');
    expect(panel.hasAttribute('hidden')).toBe(true);
    expect(panel.querySelector('a[href="/boys/tees-and-shirts"]')).not.toBeNull();
  });

  it('SC-3a a click opens the entry panel; hover alone does not', () => {
    const f = render();
    const boys = qa(f, '.rp-nav button')[1];
    boys.dispatchEvent(new MouseEvent('mouseover', { bubbles: true }));
    f.detectChanges();
    expect(boys.getAttribute('aria-expanded')).toBe('false');
    boys.click();
    f.detectChanges();
    expect(boys.getAttribute('aria-expanded')).toBe('true');
    expect(q(f, '#mega-boys').hasAttribute('hidden')).toBe(false);
    expect(q(f, '#mega-girls').hasAttribute('hidden')).toBe(true);
  });

  it('SC-6 a second click, Escape or a click outside closes the panel; Escape returns focus to the trigger', () => {
    const f = render();
    const boys = qa(f, '.rp-nav button')[1];
    boys.click(); f.detectChanges();
    boys.click(); f.detectChanges();
    expect(boys.getAttribute('aria-expanded')).toBe('false');

    boys.click(); f.detectChanges();
    q(f, '#mega-boys a').focus();
    q(f, '#mega-boys').dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    f.detectChanges();
    expect(boys.getAttribute('aria-expanded')).toBe('false');
    expect(document.activeElement).toBe(boys);

    boys.click(); f.detectChanges();
    document.body.click(); f.detectChanges();
    expect(boys.getAttribute('aria-expanded')).toBe('false');
  });

  it('SC-6 choosing a link in the panel closes it', () => {
    const f = render();
    const boys = qa(f, '.rp-nav button')[1];
    boys.click(); f.detectChanges();
    q(f, '#mega-boys a').click(); f.detectChanges();
    expect(boys.getAttribute('aria-expanded')).toBe('false');
  });

  it('SC-10 the desktop search field is a plain GET form to /search with a q input', () => {
    const f = render();
    const form = q(f, 'form.rp-search');
    expect(form.getAttribute('action')).toBe('/search');
    expect(form.getAttribute('method')).toBe('get');
    expect(form.getAttribute('role')).toBe('search');
    const input = form.querySelector('input')!;
    expect(input.getAttribute('name')).toBe('q');
    expect(input.getAttribute('type')).toBe('search');
    expect(input.getAttribute('aria-label')).toBe('Search');
  });

  it('SC-7 the menu and search icon buttons emit so the shell can open the dialogs', () => {
    const f = render();
    let menu = 0, search = 0;
    f.componentInstance.menuOpen.subscribe(() => menu++);
    f.componentInstance.searchOpen.subscribe(() => search++);
    q(f, '.rp-menu-btn').click();
    q(f, '.rp-search-btn').click();
    expect([menu, search]).toEqual([1, 1]);
  });

  it('NAV-5 follows the menu from the server: an audience, collection or sale that is gone leaves the nav with no code change', () => {
    const f = render({ ...sampleMenu, audiences: sampleMenu.audiences.slice(0, 1), collections: [], hasSale: false });
    expect(qa(f, '.rp-nav > li').map((li) => li.textContent!.trim())).toEqual(['Girls', 'New in']);
  });

  it('NAV-8 the age links in the panel carry the age code as a query parameter on the audience page', () => {
    const f = render();
    expect(q(f, '#mega-girls a[href="/girls?age=age-2y"]')).not.toBeNull();
  });

  it('NAV-3 an empty shop has an empty nav, not a made-up one', () => {
    const f = render({ audiences: [], collections: [], hasNewIn: false, hasSale: false });
    expect(qa(f, '.rp-nav > li')).toEqual([]);
  });

  it('SC-16 icon buttons have accessible names', () => {
    const f = render();
    expect(q(f, '.rp-menu-btn').getAttribute('aria-label')).toBe('Open menu');
    expect(q(f, '.rp-search-btn').getAttribute('aria-label')).toBe('Search');
  });

  it('CR-5 has no account or bag control and no newsletter form until those features exist', () => {
    const f = render();
    const labels = qa(f, 'button, a').map((e) => (e.getAttribute('aria-label') ?? '').toLowerCase());
    expect(labels.some((l) => l.includes('account') || l.includes('bag'))).toBe(false);
    expect(q(f, 'input[type="email"]')).toBeNull();
  });
});
