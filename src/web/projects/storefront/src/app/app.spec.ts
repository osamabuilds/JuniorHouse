import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideStore } from '@ngrx/store';
import { provideEffects } from '@ngrx/effects';
import { provideHttpClient } from '@angular/common/http';
import { provideSearchSuggest } from './features/storefront/search-suggest';
import { provideSiteSettings } from './features/storefront/site-settings';
import { App } from './app';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([]), provideHttpClient(), provideStore({}), provideEffects(), provideSiteSettings(), provideSearchSuggest()],
    }).compileComponents();
  });

  async function render(): Promise<HTMLElement> {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  it('DSF-14 renders the shell with the root class and without the scaffold placeholder', async () => {
    const el = await render();
    expect(el.classList.contains('rp-root')).toBe(true);
    expect(el.textContent).not.toContain('Hello, storefront');
    expect(el.querySelector('router-outlet')).not.toBeNull();
  });

  it('SC-1 has exactly one header, one main and one footer, and the primary nav', async () => {
    const el = await render();
    expect(el.querySelectorAll('header').length).toBe(1);
    expect(el.querySelectorAll('main').length).toBe(1);
    expect(el.querySelectorAll('footer').length).toBe(1);
    expect(el.querySelector('nav[aria-label="Primary"]')).not.toBeNull();
    expect([...el.querySelectorAll('.rp-nav > li')].map((li) => li.textContent!.trim())).toEqual(['Girls', 'Boys', 'New in', 'Collections', 'Sale']);
  });

  it('SC-1 the first focusable element is a skip link to the main landmark', async () => {
    const el = await render();
    const skip = el.querySelector<HTMLAnchorElement>('a.rp-skip')!;
    expect(skip.getAttribute('href')).toBe('#main');
    expect(el.querySelector('main')!.id).toBe('main');
    expect(el.querySelector('a, button')).toBe(skip);
  });
});
