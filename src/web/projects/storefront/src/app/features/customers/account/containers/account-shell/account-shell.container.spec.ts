import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AccountShellContainer } from './account-shell.container';

describe('AccountShellContainer', () => {
  it('ADD-8 offers a labelled account navigation with a link to Addresses and hosts the page', () => {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(AccountShellContainer);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;

    const link = el.querySelector<HTMLAnchorElement>('nav[aria-label="Account"] ul > li > a');
    expect(link!.textContent).toContain('Addresses');
    expect(link!.getAttribute('href')).toBe('/account/addresses');
    const prefs = el.querySelectorAll<HTMLAnchorElement>('nav[aria-label="Account"] ul > li > a')[1];
    expect(prefs.textContent).toContain('Preferences');
    expect(prefs.getAttribute('href')).toBe('/account/preferences');
    expect(el.querySelector('router-outlet')).toBeTruthy();
    expect(el.querySelectorAll('main').length).toBe(0);
  });
});
