import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Store, provideStore } from '@ngrx/store';
import { NavigationActions, provideNavigation } from '../../features/storefront/navigation';
import { sampleMenu } from '../../features/storefront/navigation/testing/sample-menu';
import { SiteSettingsActions, provideSiteSettings } from '../../features/storefront/site-settings';
import { launchSettings } from '../../shared/data/launch-settings';
import { polyfillDialog } from '../dialog/dialog.testing';
import { MobileMenuComponent } from './mobile-menu.component';

function render(): ComponentFixture<MobileMenuComponent> {
  TestBed.configureTestingModule({ providers: [provideRouter([{ path: '**', children: [] }]), provideStore({}), provideSiteSettings(), provideNavigation()] });
  const store = TestBed.inject(Store);
  store.dispatch(SiteSettingsActions.loadSuccess({ settings: launchSettings, source: 'api' }));
  store.dispatch(NavigationActions.loadMenuSuccess({ menu: sampleMenu }));
  const f = TestBed.createComponent(MobileMenuComponent);
  f.detectChanges();
  return f;
}
const dlg = (f: ComponentFixture<unknown>) => (f.nativeElement as HTMLElement).querySelector('dialog')!;

describe('MobileMenuComponent', () => {
  beforeAll(polyfillDialog);

  it('SC-7 is a labelled modal dialog that opens and closes', () => {
    const f = render();
    expect(dlg(f).getAttribute('aria-label')).toBe('Menu');
    f.componentInstance.open();
    expect(dlg(f).hasAttribute('open')).toBe(true);
    (f.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('.rp-icon-btn')!.click();
    expect(dlg(f).hasAttribute('open')).toBe(false);
  });

  it('SC-7 shows every enabled entry, with members as nested accordions', () => {
    const f = render();
    const el = f.nativeElement as HTMLElement;
    const summaries = [...el.querySelectorAll('summary')].map((s) => s.textContent!.trim());
    expect(summaries).toEqual(expect.arrayContaining(['Girls', 'Boys', 'Categories']));
    const hrefs = [...el.querySelectorAll('a')].map((a) => a.getAttribute('href'));
    expect(hrefs).toEqual(expect.arrayContaining(['/new-in', '/sale', '/boys/tees-and-shirts']));
  });

  it('SC-10 holds a plain GET search form at the top', () => {
    const f = render();
    const form = (f.nativeElement as HTMLElement).querySelector('form')!;
    expect(form.getAttribute('action')).toBe('/search');
    expect(form.getAttribute('method')).toBe('get');
    expect(form.querySelector('input')!.getAttribute('aria-label')).toBe('Search');
  });

  it('SC-8 choosing a link closes the menu', async () => {
    const f = render();
    f.componentInstance.open();
    (f.nativeElement as HTMLElement).querySelector<HTMLAnchorElement>('a[href="/sale"]')!.click();
    await f.whenStable();
    expect(dlg(f).hasAttribute('open')).toBe(false);
  });
});
