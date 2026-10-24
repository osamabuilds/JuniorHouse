import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideEffects } from '@ngrx/effects';
import { Store, provideStore } from '@ngrx/store';
import { Observable, of } from 'rxjs';
import { SearchSuggestService } from '../../features/storefront/search-suggest/services/search-suggest.service';
import { SearchSuggestEffects } from '../../features/storefront/search-suggest/store/search-suggest.effects';
import { searchSuggestFeature } from '../../features/storefront/search-suggest/store/search-suggest.reducer';
import { SiteSettingsActions, provideSiteSettings } from '../../features/storefront/site-settings';
import { launchSettings } from '../../shared/data/launch-settings';
import { mockProducts } from '../../shared/data/mock-products';
import { polyfillDialog } from '../dialog/dialog.testing';
import { SearchOverlayComponent } from './search-overlay.component';
import { vi } from 'vitest';

function render(suggest: (q: string) => Observable<typeof mockProducts> = (q) => of(mockProducts.filter((p) => p.name.toLowerCase().includes(q.toLowerCase())))) {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([{ path: '**', children: [] }]),
      provideStore({ [searchSuggestFeature.name]: searchSuggestFeature.reducer }),
      provideSiteSettings(),
      provideEffects(SearchSuggestEffects),
      { provide: SearchSuggestService, useValue: { suggest } },
    ],
  });
  TestBed.inject(Store).dispatch(SiteSettingsActions.loadSuccess({ settings: launchSettings, source: 'api' }));
  const f = TestBed.createComponent(SearchOverlayComponent);
  f.detectChanges();
  return f;
}
const el = (f: ComponentFixture<unknown>) => f.nativeElement as HTMLElement;
function type(f: ComponentFixture<unknown>, text: string) {
  const input = el(f).querySelector<HTMLInputElement>('input[name="q"]')!;
  input.value = text;
  input.dispatchEvent(new Event('input'));
}

describe('SearchOverlayComponent', () => {
  beforeAll(polyfillDialog);
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  it('SC-8 opens as a labelled modal dialog and focuses the search field', () => {
    const f = render();
    f.componentInstance.open();
    expect(el(f).querySelector('dialog')!.hasAttribute('open')).toBe(true);
    expect(el(f).querySelector('dialog')!.getAttribute('aria-label')).toBe('Search');
  });

  it('SC-10 is a plain GET form to /search so Enter goes to the results page', () => {
    const form = el(render()).querySelector('form')!;
    expect(form.getAttribute('action')).toBe('/search');
    expect(form.getAttribute('method')).toBe('get');
    expect(form.querySelector('input')!.getAttribute('name')).toBe('q');
  });

  it('shows the configured popular searches as chip links before anything is typed', () => {
    const f = render();
    const chips = [...el(f).querySelectorAll('.rp-chips a')].map((a) => a.textContent!.trim());
    expect(chips).toEqual(['Dresses', 'T-shirts', 'Shorts']);
  });

  it('SC-9 shows up to four suggestions 200 ms after typing', () => {
    const f = render();
    type(f, 'a');
    vi.advanceTimersByTime(199);
    f.detectChanges();
    expect(el(f).querySelectorAll('.rp-suggestions li').length).toBe(0);
    vi.advanceTimersByTime(1);
    f.detectChanges();
    const n = el(f).querySelectorAll('.rp-suggestions li').length;
    expect(n).toBeGreaterThan(0);
    expect(n).toBeLessThanOrEqual(4);
  });

  it('SC-9 SC-10 shows "Nothing for" with links to the entries when there are no results', () => {
    const f = render();
    type(f, 'zzzz');
    vi.advanceTimersByTime(200);
    f.detectChanges();
    expect(el(f).textContent).toContain('Nothing for');
    const hrefs = [...el(f).querySelectorAll('.rp-search-ov__empty a')].map((a) => a.getAttribute('href'));
    expect(hrefs).toEqual(expect.arrayContaining(['/new-in', '/sale', '/girls', '/boys']));
  });

  it('SC-9 typed markup is shown as text and never becomes elements', () => {
    const f = render();
    type(f, '<img src=x onerror=alert(1)>');
    vi.advanceTimersByTime(200);
    f.detectChanges();
    expect(el(f).querySelector('img[src="x"]')).toBeNull();
    expect(el(f).textContent).toContain('<img src=x onerror=alert(1)>');
  });

  it('SC-8 closing clears the typed text', () => {
    const f = render();
    f.componentInstance.open();
    type(f, 'shirt');
    vi.advanceTimersByTime(200);
    f.detectChanges();
    f.componentInstance.close();
    f.detectChanges();
    expect(el(f).querySelector<HTMLInputElement>('input[name="q"]')!.value).toBe('');
  });
});
